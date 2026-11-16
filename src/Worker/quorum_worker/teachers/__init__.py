"""
Учителя: по имени из реестра Labeling выбирается реализация.

Имя учителя задаёт оператор в POST /teachers, поэтому сопоставление идёт по префиксу
(суффиксы вроде «-1234» из демо-скрипта не мешают):

  OpenVocabDetector:  grounding-dino[-tiny|-base]   → IDEA-Research/grounding-dino-*
                      mm-grounding-dino[-base|-large] → openmmlab-community/mm_grounding_dino_*_all
                      llmdet[-large]                → iSEE-Laboratory/llmdet_large
                      yolo-world[-s|-m|-l|-x]       → Ultralytics yolov8*-worldv2.pt
                      owlv2[-base|-large]           → google/owlv2-*-ensemble
  Detector:           <имя файла весов в MODELS_DIR без .pt>, например yolov8l-plates
  Vlm:                имя модели на OpenAI-совместимом сервере (llama-swap / llama.cpp),
                      можно переопределить картой TEACHER_MAP_FILE (JSON: {"имя учителя": "id модели"})
"""
from __future__ import annotations

from .base import Teacher, TeacherError, FramePrediction


def resolve(name: str, kind: str) -> Teacher:
    from . import detectors, vlm

    if kind == "Vlm":
        return vlm.VlmTeacher(name)
    if kind == "Detector":
        return detectors.LocalYoloTeacher(name)
    if kind == "OpenVocabDetector":
        return detectors.open_vocab(name)
    raise TeacherError(f"Unknown teacher kind {kind!r}")


__all__ = ["Teacher", "TeacherError", "FramePrediction", "resolve"]
