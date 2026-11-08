"""
Мультимодальные модели — учителя для распознавания текста на кропах.

Модели не живут в воркере: их держит OpenAI-совместимый сервер (llama-swap поверх llama.cpp),
который сам выгружает одну модель и подгружает другую по полю "model" в запросе. Это и есть
«одна модель в памяти за раз» для VLM — 16 ГБ не вмещают две модели 8–9B.

Модель не отдаёт вероятностей, поэтому confidence = 1 для прочитанного и 0 для отказа;
надёжность метки берётся из согласия ансамбля, а не из модели.
"""
from __future__ import annotations

import base64
import json
import logging
import os
import re
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Iterator

from ..common import clean_vlm_answer, resolve_frame
from .base import FramePrediction, Teacher, TeacherError

log = logging.getLogger("quorum.worker.vlm")

BASE_URL = os.getenv("VLM_BASE_URL", "http://llm:8080/v1").rstrip("/")
TIMEOUT = float(os.getenv("VLM_TIMEOUT", "300"))
PARALLEL = int(os.getenv("VLM_PARALLEL", "2"))
REQUIRE_DIGITS = int(os.getenv("OCR_REQUIRE_DIGITS", "3"))

PROMPT = (
    "Read the text on this {subject}. Answer with the characters exactly as written, "
    "without spaces, punctuation or explanations. If the text is unreadable, answer: NONE."
)
MIME = {".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".png": "image/png", ".webp": "image/webp", ".bmp": "image/bmp"}


def _model_map() -> dict[str, str]:
    path = os.getenv("TEACHER_MAP_FILE")
    if not path:
        return {}
    return json.loads(Path(path).read_text(encoding="utf-8"))


class VlmTeacher(Teacher):
    def __init__(self, name: str) -> None:
        super().__init__(name)
        mapping = _model_map()
        self.model = mapping.get(name) or mapping.get(re.sub(r"-\d+$", "", name)) or re.sub(r"-\d+$", "", name)

    @property
    def model_key(self) -> str:
        return f"vlm:{self.model}"  # VRAM держит внешний сервер; слот воркера VLM не занимает

    def _ask(self, session, path: Path, prompt: str) -> str:
        payload = {
            "model": self.model,
            "messages": [{"role": "user", "content": [
                {"type": "image_url", "image_url": {"url": f"data:{MIME.get(path.suffix.lower(), 'image/jpeg')};base64,"
                                                          f"{base64.b64encode(path.read_bytes()).decode()}"}},
                {"type": "text", "text": prompt},
            ]}],
            # Нулевая температура: разметка должна воспроизводиться, иначе повторный прогон даст другие метки.
            "temperature": 0.0,
            "max_tokens": 32,
        }
        r = session.post(f"{BASE_URL}/chat/completions", json=payload, timeout=TIMEOUT)
        if r.status_code in (404, 400) and "model" in r.text.lower():
            raise TeacherError(f"Model {self.model!r} is not served at {BASE_URL}: {r.text[:200]}")
        r.raise_for_status()
        return r.json()["choices"][0]["message"].get("content") or ""

    def predict(self, frames, prompt) -> Iterator[FramePrediction]:
        import requests

        session = requests.Session()
        question = PROMPT.format(subject=(prompt or "license plate").strip())

        def one(item: tuple[str, str]) -> FramePrediction:
            frame_id, rel = item
            path = resolve_frame(rel)
            if not path.is_file():
                return FramePrediction(frame_id, error="file not found")
            try:
                raw = self._ask(session, path, question)
            except TeacherError:
                raise
            except Exception as e:  # таймаут на одном кадре — «не прочитал», а не падение прогона
                log.warning("%s failed on %s: %s", self.model, frame_id, e)
                return FramePrediction(frame_id, error=str(e))
            text = clean_vlm_answer(raw, REQUIRE_DIGITS)
            return FramePrediction(frame_id, text=text, confidence=1.0 if text else 0.0)

        try:
            session.get(f"{BASE_URL}/models", timeout=10).raise_for_status()
        except Exception as e:
            raise TeacherError(f"VLM server {BASE_URL} is unavailable: {e}") from e

        with ThreadPoolExecutor(max_workers=PARALLEL) as pool:
            yield from pool.map(one, frames)
