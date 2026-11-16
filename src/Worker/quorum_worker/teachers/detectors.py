"""
Детекторы-учителя: три open-vocabulary модели (поиск по текстовому запросу, без обучения на классе —
решают холодный старт) и обычные YOLO с весами из прошлых работ (со второй итерации, когда есть
на чём учить). Три open-vocab модели разной архитектуры — чтобы ошибки не коррелировали:
согласие одинаково устроенных моделей ничего не говорит о правильности метки.
"""
from __future__ import annotations

import os
import re
from pathlib import Path
from typing import Iterator

from .. import vram
from ..common import Box, box_from_xyxy, resolve_frame
from .base import FramePrediction, Teacher, TeacherError, confidence_of, load_images

MODELS_DIR = Path(os.getenv("MODELS_DIR", "/models"))


def _threshold(env: str, default: float) -> float:
    return float(os.getenv(env, os.getenv("DETECTOR_SCORE_THRESHOLD", default)))


def _max_area(env: str) -> float:
    """
    Верхняя граница площади бокса (доля кадра), по умолчанию без ограничения.
    Априорное знание о классе: номер никогда не занимает полкадра, а GroundingDINO по запросу
    «license plate» уверенно обводит всю машину. Для номеров разумно 0.1 (см. src/Worker/README.md).
    """
    return float(os.getenv(env, os.getenv("DETECTOR_MAX_BOX_AREA", "1.0")))


def _limit(boxes: list[Box], max_area: float) -> list[Box]:
    return boxes if max_area >= 1.0 else [b for b in boxes if b.area <= max_area]


def _prompt_classes(prompt: str) -> list[str]:
    classes = [c.strip() for c in (prompt or "").split(",") if c.strip()]
    return classes or ["object"]


def open_vocab(name: str) -> Teacher:
    n = name.lower()
    # Потомки GroundingDINO с тем же интерфейсом в transformers (нужен transformers ≥ 4.55).
    if n.startswith(("mm-grounding-dino", "mm_grounding_dino", "mmgroundingdino")):
        size = "base" if "base" in n else "large"
        return GroundingDinoTeacher(name, model_id=f"openmmlab-community/mm_grounding_dino_{size}_all")
    if n.startswith("llmdet"):
        return GroundingDinoTeacher(name, model_id="iSEE-Laboratory/llmdet_large")
    if n.startswith(("grounding-dino", "groundingdino", "grounding_dino")):
        return GroundingDinoTeacher(name, tiny="tiny" in n)
    if n.startswith(("yolo-world", "yoloworld", "yolo_world")):
        m = re.match(r"yolo[-_]?world[-_]?([smlx])\b", n)
        return YoloWorldTeacher(name, size=m.group(1) if m else "l")
    if n.startswith("owlv2") or n.startswith("owl-v2"):
        return Owlv2Teacher(name, large="large" in n)
    raise TeacherError(f"Unknown open-vocabulary teacher {name!r}: expected grounding-dino*, mm-grounding-dino*, "
                       f"llmdet*, yolo-world*, owlv2*")


class _HfDetector(Teacher):
    """Общее для моделей из transformers: загрузка в слот, fp16 на GPU, покадровый инференс."""
    model_id: str

    @property
    def model_key(self) -> str:
        return self.model_id

    def _load(self, model_cls):
        import torch
        from transformers import AutoProcessor

        dev = vram.device()
        model = model_cls.from_pretrained(self.model_id, torch_dtype=torch.float16 if dev == "cuda" else torch.float32)
        return AutoProcessor.from_pretrained(self.model_id), model.to(dev).eval()

    def predict(self, frames, prompt) -> Iterator[FramePrediction]:
        import torch

        processor, model = vram.slot.acquire(self.model_key, self.load)
        for frame_id, image, error in load_images(frames):
            if image is None:
                yield FramePrediction(frame_id, error=error)
                continue
            with torch.inference_mode():
                boxes = _limit(self._detect(processor, model, image, prompt), self.max_area)
            yield FramePrediction(frame_id, boxes, confidence=confidence_of(boxes))

    def load(self):
        raise NotImplementedError

    def _detect(self, processor, model, image, prompt) -> list[Box]:
        raise NotImplementedError


class GroundingDinoTeacher(_HfDetector):
    def __init__(self, name: str, tiny: bool = False, model_id: str | None = None) -> None:
        super().__init__(name)
        # model_id — другой чекпойнт той же архитектуры (MM-GroundingDINO, LLMDet): запрос, пороги
        # и постобработка у них общие с GroundingDINO, поэтому и переменные окружения те же (GDINO_*).
        self.model_id = model_id or f"IDEA-Research/grounding-dino-{'tiny' if tiny else 'base'}"
        self.threshold = _threshold("GDINO_THRESHOLD", 0.3)
        self.max_area = _max_area("GDINO_MAX_BOX_AREA")

    def load(self):
        from transformers import AutoModelForZeroShotObjectDetection
        return self._load(AutoModelForZeroShotObjectDetection)

    def _detect(self, processor, model, image, prompt):
        # Формат запроса GroundingDINO: классы в нижнем регистре, каждый заканчивается точкой.
        text = " ".join(f"{c.lower()}." for c in _prompt_classes(prompt))
        inputs = processor(images=image, text=text, return_tensors="pt").to(model.device)
        if "pixel_values" in inputs:
            inputs["pixel_values"] = inputs["pixel_values"].to(model.dtype)
        outputs = model(**inputs)
        w, h = image.size
        kwargs = dict(text_threshold=self.threshold, target_sizes=[(h, w)])
        try:
            result = processor.post_process_grounded_object_detection(outputs, inputs.input_ids, threshold=self.threshold, **kwargs)[0]
        except TypeError:  # transformers < 4.51 называл порог box_threshold
            result = processor.post_process_grounded_object_detection(outputs, inputs.input_ids, box_threshold=self.threshold, **kwargs)[0]
        return [b for b in (box_from_xyxy(*xyxy, w, h, s) for xyxy, s in
                            zip(result["boxes"].float().tolist(), result["scores"].float().tolist())) if b]


class Owlv2Teacher(_HfDetector):
    def __init__(self, name: str, large: bool = False) -> None:
        super().__init__(name)
        self.model_id = "google/owlv2-large-patch14-ensemble" if large else "google/owlv2-base-patch16-ensemble"
        # У OWLv2 уверенности заметно ниже, чем у GroundingDINO, — свой порог.
        self.threshold = _threshold("OWLV2_THRESHOLD", 0.15)
        self.max_area = _max_area("OWLV2_MAX_BOX_AREA")

    def load(self):
        from transformers import Owlv2ForObjectDetection
        return self._load(Owlv2ForObjectDetection)

    def _detect(self, processor, model, image, prompt):
        queries = [[f"a photo of a {c}" for c in _prompt_classes(prompt)]]
        inputs = processor(text=queries, images=image, return_tensors="pt").to(model.device)
        inputs["pixel_values"] = inputs["pixel_values"].to(model.dtype)
        outputs = model(**inputs)
        w, h = image.size
        # OWLv2 дополняет кадр до квадрата справа и снизу, поэтому координаты — в квадрате со стороной max(w, h).
        side = max(w, h)
        result = processor.post_process_grounded_object_detection(outputs, threshold=self.threshold, target_sizes=[(side, side)])[0]
        return [b for b in (box_from_xyxy(*xyxy, w, h, s) for xyxy, s in
                            zip(result["boxes"].float().tolist(), result["scores"].float().tolist())) if b]


class _UltralyticsTeacher(Teacher):
    batch = int(os.getenv("YOLO_TEACHER_BATCH", "16"))
    threshold = 0.25
    max_area = 1.0

    @property
    def model_key(self) -> str:
        return self.weights

    def _model(self, prompt: str):
        return vram.slot.acquire(self.model_key, self.load)

    def load(self):
        from ultralytics import YOLO
        return YOLO(self.weights)

    def predict(self, frames, prompt) -> Iterator[FramePrediction]:
        model = self._model(prompt)
        dev = vram.device()
        for start in range(0, len(frames), self.batch):
            chunk = frames[start:start + self.batch]
            readable, broken = [], []
            for frame_id, path in chunk:
                (readable if resolve_frame(path).is_file() else broken).append((frame_id, path))
            for frame_id, _ in broken:
                yield FramePrediction(frame_id, error="file not found")
            if not readable:
                continue
            try:
                extra = {"half": True} if dev == "cuda" else {}
                results = model.predict([str(resolve_frame(p)) for _, p in readable], conf=self.threshold,
                                        device=0 if dev == "cuda" else dev, verbose=False, **extra)
            except Exception as e:  # битый файл в батче — переходим на покадровый режим
                results = []
                for item in readable:
                    try:
                        results.append(model.predict(str(resolve_frame(item[1])), conf=self.threshold, verbose=False)[0])
                    except Exception as inner:
                        results.append(inner)
            for (frame_id, _), r in zip(readable, results):
                if isinstance(r, Exception):
                    yield FramePrediction(frame_id, error=str(r))
                    continue
                h, w = r.orig_shape
                boxes = _limit([b for b in (box_from_xyxy(*xyxy, w, h, s) for xyxy, s in
                                            zip(r.boxes.xyxy.tolist(), r.boxes.conf.tolist())) if b], self.max_area)
                yield FramePrediction(frame_id, boxes, confidence=confidence_of(boxes))


class YoloWorldTeacher(_UltralyticsTeacher):
    def __init__(self, name: str, size: str = "l") -> None:
        super().__init__(name)
        self.weights = os.getenv(f"YOLO_WORLD_{size.upper()}_WEIGHTS", f"yolov8{size}-worldv2.pt")
        self.threshold = _threshold("YOLO_WORLD_THRESHOLD", 0.1)
        self.max_area = _max_area("YOLO_WORLD_MAX_BOX_AREA")
        self._classes: list[str] | None = None

    def _model(self, prompt: str):
        model = vram.slot.acquire(self.model_key, self.load)
        classes = _prompt_classes(prompt)
        if classes != self._classes:
            model.set_classes(classes)
            self._classes = classes
        return model


class LocalYoloTeacher(_UltralyticsTeacher):
    """Обычный YOLO с весами из MODELS_DIR: models/<имя>.pt. Суффикс «-123» в имени учителя отбрасывается."""

    def __init__(self, name: str) -> None:
        super().__init__(name)
        candidates = [name, re.sub(r"-\d+$", "", name)]
        found = next((MODELS_DIR / f"{c}.pt" for c in candidates if (MODELS_DIR / f"{c}.pt").is_file()), None)
        if found is None:
            raise TeacherError(f"Weights for detector {name!r} not found: put {MODELS_DIR / (name + '.pt')}")
        self.weights = str(found)
        self.threshold = _threshold("YOLO_THRESHOLD", 0.25)
