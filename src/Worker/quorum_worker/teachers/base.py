from __future__ import annotations

import abc
import logging
from dataclasses import dataclass, field
from typing import Iterable, Iterator

from ..common import Box, resolve_frame

log = logging.getLogger("quorum.worker.teachers")


class TeacherError(Exception):
    """Учитель не может работать вообще (нет весов, нет сервера). Прерывает прогон — Labeling запустит компенсацию."""


@dataclass
class FramePrediction:
    frame_id: str
    boxes: list[Box] = field(default_factory=list)
    text: str = ""
    confidence: float = 0.0
    error: str | None = None  # кадр не прочитался — голос «ничего нет», но в логах видно почему


class Teacher(abc.ABC):
    """Один учитель. Предсказания отдаются генератором по мере готовности — Labeling пишет их в кэш потоком."""

    def __init__(self, name: str) -> None:
        self.name = name

    @property
    @abc.abstractmethod
    def model_key(self) -> str:
        """Ключ для слота видеопамяти: одинаковые модели под разными именами не грузятся дважды."""

    @abc.abstractmethod
    def predict(self, frames: list[tuple[str, str]], prompt: str) -> Iterator[FramePrediction]:
        """frames — пары (frame_id, path). path относительный к FRAMES_ROOT или абсолютный."""


def load_images(frames: Iterable[tuple[str, str]]):
    """(frame_id, PIL.Image | None, ошибка). Битый кадр не роняет прогон: в демо, например, грузят случайные байты."""
    from PIL import Image, UnidentifiedImageError

    for frame_id, path in frames:
        try:
            with Image.open(resolve_frame(path)) as img:
                yield frame_id, img.convert("RGB"), None
        except (OSError, UnidentifiedImageError) as e:
            log.warning("Frame %s unreadable (%s): %s", frame_id, path, e)
            yield frame_id, None, str(e)


def confidence_of(boxes: list[Box]) -> float:
    return round(sum(b.score for b in boxes) / len(boxes), 4) if boxes else 0.0
