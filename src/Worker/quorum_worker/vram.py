"""
Одна тяжёлая модель в памяти за раз.

Целевая машина — 16 ГБ видеопамяти: два детектора-учителя плюс обучение студента туда не влезают.
Labeling и так прогоняет учителей по очереди (всего учителя по всем кадрам, затем следующего),
поэтому достаточно держать последнюю загруженную модель и выгружать её при смене.
"""
from __future__ import annotations

import gc
import logging
import threading
from typing import Any, Callable

log = logging.getLogger("quorum.worker.vram")


def device() -> str:
    try:
        import torch
    except ImportError:
        return "cpu"
    if torch.cuda.is_available():
        return "cuda"
    if getattr(torch.backends, "mps", None) and torch.backends.mps.is_available():
        return "mps"
    return "cpu"


class ModelSlot:
    def __init__(self) -> None:
        self._key: str | None = None
        self._model: Any = None
        self._lock = threading.RLock()

    def acquire(self, key: str, loader: Callable[[], Any]) -> Any:
        with self._lock:
            if self._key == key:
                return self._model
            self.release()
            log.info("Loading %s on %s", key, device())
            self._model = loader()
            self._key = key
            return self._model

    def release(self) -> None:
        with self._lock:
            if self._key is not None:
                log.info("Unloading %s", self._key)
            self._key, self._model = None, None
            gc.collect()
            try:
                import torch
                if torch.cuda.is_available():
                    torch.cuda.empty_cache()
            except ImportError:
                pass


slot = ModelSlot()
