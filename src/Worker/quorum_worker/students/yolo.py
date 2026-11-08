"""
YOLO-студент для детекции (Ultralytics).

Датасет собирается в каталоге задачи из нормированных боксов TrainRequest: изображения — символьные
ссылки на кадры в томе frames (копировать гигабайты на каждую итерацию незачем), метки — в формате YOLO.
Обучение идёт в отдельном потоке, эпохи пробрасываются наружу через очередь, чтобы gRPC-стрим
отдавал прогресс в реальном времени.
"""
from __future__ import annotations

import logging
import os
import queue
import shutil
import threading
from pathlib import Path
from typing import Iterator

from .. import vram
from ..common import resolve_frame
from . import Epoch, Sample, Trained, validation_split

log = logging.getLogger("quorum.worker.yolo")

IMGSZ = int(os.getenv("YOLO_IMGSZ", "640"))
BATCH = int(os.getenv("YOLO_BATCH", "16"))
WORKERS = int(os.getenv("YOLO_DATALOADER_WORKERS", "4"))
METRIC = "mAP50"


def _device():
    dev = vram.device()
    return 0 if dev == "cuda" else dev


def _total_loss(tloss) -> float:
    """Ultralytics 8.4+ хранит средние потери словарём {box, cls, dfl}, более ранние — тензором."""
    if tloss is None:
        return 0.0
    if isinstance(tloss, dict):
        return float(sum(float(v) for v in tloss.values()))
    return float(tloss.sum())


def write_dataset(root: Path, splits: dict[str, list[Sample]]) -> Path:
    """Каталог YOLO: images/<split> (ссылки), labels/<split> (cx cy w h), data.yaml. Кадр без боксов — негативный пример."""
    if root.exists():
        shutil.rmtree(root)
    for split, samples in splits.items():
        images, labels = root / "images" / split, root / "labels" / split
        images.mkdir(parents=True)
        labels.mkdir(parents=True)
        for s in samples:
            src = resolve_frame(s.path)
            if not src.is_file():
                log.warning("Skip missing frame %s (%s)", s.frame_id, src)
                continue
            # Имя по frame_id: у разных камер могут совпадать имена файлов.
            stem = s.frame_id
            dst = images / f"{stem}{src.suffix.lower() or '.jpg'}"
            try:
                dst.symlink_to(src.resolve())
            except OSError:
                shutil.copy2(src, dst)
            (labels / f"{stem}.txt").write_text("".join(
                f"0 {b.x + b.w / 2:.6f} {b.y + b.h / 2:.6f} {b.w:.6f} {b.h:.6f}\n" for b in s.boxes))
    first = next(iter(splits))
    val = "val" if "val" in splits else first
    (root / "data.yaml").write_text(
        f"path: {root}\ntrain: images/{first}\nval: images/{val}\nnames:\n  0: object\n")
    return root / "data.yaml"


def train(job_dir: Path, samples: list[Sample], architecture: str, epochs: int, base_artifact: str,
          artifact_path: Path) -> Iterator[Epoch | Trained]:
    from ultralytics import YOLO

    vram.slot.release()  # учителя больше не нужны — обучению нужна вся видеопамять
    train_set, val_set = validation_split(samples)
    data = write_dataset(job_dir / "dataset", {"train": train_set, "val": val_set})

    # Дообучение от опубликованной версии: петля накапливает знание, а не учится заново каждый цикл.
    start = base_artifact if base_artifact.endswith(".pt") and Path(base_artifact).is_file() else f"{architecture}.pt"
    log.info("YOLO train: %d train / %d val, from %s", len(train_set), len(val_set), start)
    model = YOLO(start)

    events: queue.Queue = queue.Queue()
    outcome: dict = {}

    reported: set[int] = set()

    def on_epoch_end(trainer) -> None:
        # Колбэк срабатывает ещё раз на финальной валидации лучших весов — уже после последней эпохи.
        # «Эпоха 4 из 3» сломала бы прогресс TrainingJob в .NET, поэтому отдаём каждую эпоху ровно один раз.
        epoch = min(trainer.epoch + 1, trainer.epochs)
        if epoch in reported:
            return
        reported.add(epoch)
        events.put(Epoch(epoch, trainer.epochs, round(_total_loss(getattr(trainer, "tloss", None)), 4)))

    model.add_callback("on_fit_epoch_end", on_epoch_end)

    def run() -> None:
        try:
            model.train(data=str(data), epochs=epochs, imgsz=IMGSZ, batch=min(BATCH, max(2, len(train_set))),
                        device=_device(), workers=WORKERS, project=str(job_dir), name="run", exist_ok=True,
                        plots=False, verbose=False, amp=vram.device() == "cuda")
            outcome["trainer"] = model.trainer
        except BaseException as e:  # noqa: BLE001 — ошибку отдаём в поток gRPC
            outcome["error"] = e
        finally:
            events.put(None)

    threading.Thread(target=run, daemon=True, name="yolo-train").start()
    while (event := events.get()) is not None:
        yield event
    if "error" in outcome:
        raise outcome["error"]

    trainer = outcome["trainer"]
    best = Path(trainer.best) if Path(trainer.best).is_file() else Path(trainer.last)
    artifact_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(best, artifact_path)
    metric = evaluate_artifact(str(artifact_path), val_set, job_dir / "val")
    yield Trained(str(artifact_path), METRIC, metric)


def evaluate_artifact(artifact: str, samples: list[Sample], work_dir: Path) -> float:
    """mAP@0.5 на переданных кадрах. Для оценки кандидата и базовой модели на отложенной выборке цикла."""
    from ultralytics import YOLO

    data = write_dataset(work_dir, {"val": samples})
    metrics = YOLO(artifact).val(data=str(data), split="val", imgsz=IMGSZ, batch=BATCH, device=_device(),
                                 workers=0, plots=False, verbose=False, project=str(work_dir), name="metrics",
                                 exist_ok=True)
    return round(float(metrics.box.map50), 4)
