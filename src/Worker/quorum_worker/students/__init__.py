"""
Студенты — лёгкие модели, которые после обучения работают на камере в реальном времени.

Обе реализации отдают одинаковые события: прогресс эпохи (epoch, loss) и финальный результат
(путь к артефакту, имя и значение метрики). server.py превращает их в TrainProgress из worker.proto.
"""
from __future__ import annotations

from dataclasses import dataclass, field

from ..common import Box


@dataclass
class Sample:
    frame_id: str
    path: str
    boxes: list[Box] = field(default_factory=list)
    text: str = ""


@dataclass
class Epoch:
    epoch: int
    epochs: int
    loss: float


@dataclass
class Trained:
    artifact: str
    metric_name: str
    metric_value: float


def validation_split(samples: list[Sample], every: int = 10) -> tuple[list[Sample], list[Sample]]:
    """
    Детерминированно откладываем каждый N-й кадр: TrainRequest приходит без валидационной выборки,
    а она нужна, чтобы выбрать лучшую эпоху. Отложенная выборка цикла (holdout) сюда не попадает —
    по ней Training потом честно сравнивает кандидата с текущей моделью.
    """
    ordered = sorted(samples, key=lambda s: s.frame_id)
    if len(ordered) < 5:
        return ordered, ordered
    val = [s for i, s in enumerate(ordered) if i % every == every - 1] or ordered[-1:]
    val_ids = {s.frame_id for s in val}
    train = [s for s in ordered if s.frame_id not in val_ids]
    return train, val
