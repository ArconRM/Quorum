"""
Ученики-детекторы из torchvision для главного эксперимента — другие семейства, чем YOLO:

  ssdlite    SSDlite320 на MobileNetV3 (одноэтапный, вход 320×320) — та же модель, что в курсовой;
  fasterrcnn Faster R-CNN на MobileNetV3-FPN (двухэтапный) — аналог Faster R-CNN из Detectron2 в курсовой.

Обе стартуют с весов COCO (как yolov8n.pt), голова классификации заменяется на «номер / фон».
Метрики считаются здесь же, одинаково для обеих моделей: AP при IoU 0.5 и среднее AP по IoU 0.5:0.95
(101-точечная интерполяция, как в COCO), P и R — при пороге уверенности с наибольшим F1.
"""
from __future__ import annotations

import logging
import os
import random
import time
from pathlib import Path

from quorum_worker.common import resolve_frame
from quorum_worker.students import Sample, validation_split

log = logging.getLogger("experiments.tvdet")

KINDS = ("ssdlite", "fasterrcnn")
BATCH = {"ssdlite": 32, "fasterrcnn": 8}
LR = {"ssdlite": 1e-3, "fasterrcnn": 2e-4}


# ---------------- модель ----------------

def build_model(kind: str, pretrained: bool = True, imgsz: int = 640):
    import torch.nn as nn
    from functools import partial
    from torchvision.models.detection import fasterrcnn_mobilenet_v3_large_fpn, ssdlite320_mobilenet_v3_large

    if kind == "ssdlite":
        from torchvision.models.detection import _utils as det_utils
        from torchvision.models.detection.ssdlite import SSDLiteClassificationHead

        model = ssdlite320_mobilenet_v3_large(weights="DEFAULT" if pretrained else None,
                                              weights_backbone=None)
        channels = det_utils.retrieve_out_channels(model.backbone, (320, 320))
        anchors = model.anchor_generator.num_anchors_per_location()
        model.head.classification_head = SSDLiteClassificationHead(
            channels, anchors, 2, partial(nn.BatchNorm2d, eps=0.001, momentum=0.03))
        return model
    if kind == "fasterrcnn":
        from torchvision.models.detection.faster_rcnn import FastRCNNPredictor

        # Вход как у YOLO (640 по меньшей стороне): номера мелкие, при 320 Faster R-CNN их теряет.
        model = fasterrcnn_mobilenet_v3_large_fpn(weights="DEFAULT" if pretrained else None,
                                                  weights_backbone=None, min_size=imgsz,
                                                  max_size=round(imgsz * 16 / 9))
        model.roi_heads.box_predictor = FastRCNNPredictor(model.roi_heads.box_predictor.cls_score.in_features, 2)
        return model
    raise ValueError(f"Неизвестный ученик {kind!r}: есть {', '.join(KINDS)}")


# ---------------- данные ----------------

class _Frames:
    """Кадр, уменьшенный до imgsz по длинной стороне (меньше работы загрузчику), и рамки в пикселях."""

    def __init__(self, samples: list[Sample], imgsz: int, augment: bool) -> None:
        self.samples, self.imgsz, self.augment = samples, imgsz, augment

    def __len__(self) -> int:
        return len(self.samples)

    def __getitem__(self, i: int):
        import cv2
        import numpy as np
        import torch

        s = self.samples[i]
        img = cv2.imread(str(resolve_frame(s.path)), cv2.IMREAD_COLOR)
        if img is None:
            img = np.zeros((self.imgsz, self.imgsz, 3), np.uint8)
        h, w = img.shape[:2]
        k = self.imgsz / max(h, w)
        if k < 1:
            img = cv2.resize(img, (round(w * k), round(h * k)), interpolation=cv2.INTER_AREA)
            h, w = img.shape[:2]
        img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
        boxes = torch.tensor([[b.x * w, b.y * h, (b.x + b.w) * w, (b.y + b.h) * h] for b in s.boxes],
                             dtype=torch.float32).reshape(-1, 4)
        tensor = torch.from_numpy(np.ascontiguousarray(img)).permute(2, 0, 1)
        if self.augment:
            tensor, boxes = _augment(tensor, boxes)
        target = {"boxes": boxes, "labels": torch.ones(len(boxes), dtype=torch.int64)}
        return tensor.float() / 255.0, target


def _augment(image, boxes):
    """
    Стандартный рецепт обучения SSD из torchvision (references/detection): цветовые искажения,
    отдаление с полями, случайный кроп по IoU, отражение. У YOLO своя сильная аугментация (мозаика,
    масштаб); без сравнимой ученики torchvision переобучаются на почти одинаковых соседних кадрах.
    """
    from torchvision import tv_tensors
    from torchvision.transforms import v2

    h, w = image.shape[-2:]
    img = tv_tensors.Image(image)
    bb = tv_tensors.BoundingBoxes(boxes, format="XYXY", canvas_size=(h, w))
    steps = [v2.RandomPhotometricDistort(p=0.5), v2.RandomZoomOut(fill=114, side_range=(1.0, 2.0), p=0.3)]
    if len(boxes):   # кроп по IoU определён только для кадров с объектами
        steps.append(v2.RandomApply([v2.RandomIoUCrop()], p=0.5))
    steps += [v2.RandomHorizontalFlip(p=0.5), v2.SanitizeBoundingBoxes(labels_getter=None)]
    img, bb = v2.Compose(steps)(img, bb)
    return img.as_subclass(type(image)), bb.as_subclass(type(boxes)).reshape(-1, 4)


def _collate(batch):
    return tuple(zip(*batch))


def _loader(samples: list[Sample], imgsz: int, batch: int, augment: bool, shuffle: bool, persistent: bool = False):
    import torch

    # Иначе тензоры между процессами передаются через файловые дескрипторы и при 10 загрузчиках
    # упираются в их лимит («received 0 items of ancdata»).
    torch.multiprocessing.set_sharing_strategy("file_system")
    workers = int(os.getenv("YOLO_DATALOADER_WORKERS", "4"))
    return torch.utils.data.DataLoader(_Frames(samples, imgsz, augment), batch_size=batch, shuffle=shuffle,
                                       num_workers=workers, collate_fn=_collate,
                                       persistent_workers=persistent and workers > 0)


# ---------------- метрики ----------------

def _iou_matrix(a, b):
    import numpy as np

    if len(a) == 0 or len(b) == 0:
        return np.zeros((len(a), len(b)))
    a, b = np.asarray(a, float)[:, None], np.asarray(b, float)[None]
    iw = np.clip(np.minimum(a[..., 2], b[..., 2]) - np.maximum(a[..., 0], b[..., 0]), 0, None)
    ih = np.clip(np.minimum(a[..., 3], b[..., 3]) - np.maximum(a[..., 1], b[..., 1]), 0, None)
    inter = iw * ih
    area = lambda x: (x[..., 2] - x[..., 0]) * (x[..., 3] - x[..., 1])
    return inter / np.maximum(area(a) + area(b) - inter, 1e-9)


def detection_metrics(preds: list[tuple[list, list]], truths: list[list]) -> dict:
    """
    preds — по кадру (рамки xyxy, уверенности), truths — по кадру рамки xyxy.
    AP: жадное сопоставление по убыванию уверенности, 101-точечная интерполяция (COCO).
    """
    import numpy as np

    total = sum(len(t) for t in truths)
    aps, curve50 = [], None
    for thr in np.arange(0.5, 0.96, 0.05):
        scores, hits = [], []
        for (boxes, conf), gt in zip(preds, truths):
            order = np.argsort(-np.asarray(conf, float))
            ious = _iou_matrix([boxes[i] for i in order], gt)
            used = set()
            for row, i in enumerate(order):
                best, j = 0.0, -1
                for k in range(len(gt)):
                    if k not in used and ious[row, k] >= thr and ious[row, k] > best:
                        best, j = ious[row, k], k
                if j >= 0:
                    used.add(j)
                scores.append(conf[i])
                hits.append(j >= 0)
        if not scores or total == 0:
            aps.append(0.0)
            continue
        order = np.argsort(-np.asarray(scores))
        tp = np.cumsum(np.asarray(hits)[order])
        fp = np.cumsum(~np.asarray(hits)[order])
        recall, precision = tp / total, tp / np.maximum(tp + fp, 1)
        envelope = np.maximum.accumulate(precision[::-1])[::-1]
        grid = np.linspace(0, 1, 101)
        idx = np.searchsorted(recall, grid, side="left")
        aps.append(float(np.mean([envelope[i] if i < len(envelope) else 0.0 for i in idx])))
        if curve50 is None:
            curve50 = (precision, recall)
    p = r = 0.0
    if curve50 is not None:
        precision, recall = curve50
        f1 = 2 * precision * recall / np.maximum(precision + recall, 1e-9)
        best = int(np.argmax(f1))
        p, r = float(precision[best]), float(recall[best])
    return {"map50": round(aps[0], 4), "map50_95": round(float(np.mean(aps)), 4),
            "precision": round(p, 4), "recall": round(r, 4)}


def _predict(model, samples: list[Sample], imgsz: int, device) -> tuple[list, list]:
    import torch

    model.eval()
    preds, truths = [], []
    with torch.inference_mode():
        for images, targets in _loader(samples, imgsz, 8, augment=False, shuffle=False):
            for out, t in zip(model([im.to(device) for im in images]), targets):
                preds.append((out["boxes"].cpu().tolist(), out["scores"].cpu().tolist()))
                truths.append(t["boxes"].tolist())
    return preds, truths


# ---------------- обучение и оценка ----------------

def train(kind: str, job_dir: Path, samples: list[Sample], start: str, epochs: int, imgsz: int, seed: int,
          pretrained: bool = True) -> str:
    """
    Обучает ученика и возвращает путь к весам лучшей эпохи на части тех же меток. Лучшая — по той же
    «fitness», что у YOLO в Ultralytics: 0.1·mAP50 + 0.9·mAP50-95, чтобы все ученики отбирались одинаково.
    """
    import torch

    random.seed(seed)
    torch.manual_seed(seed)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model = build_model(kind, pretrained, imgsz)
    if start and start.endswith(".pt") and Path(start).is_file():
        model.load_state_dict(torch.load(start, map_location="cpu", weights_only=True))
        log.info("%s: дообучение с %s", kind, start)
    model.to(device)

    train_set, val_set = validation_split(samples)
    loader = _loader(train_set, imgsz, BATCH[kind], augment=True, shuffle=True, persistent=True)
    params = [p for p in model.parameters() if p.requires_grad]
    opt = torch.optim.AdamW(params, lr=LR[kind], weight_decay=1e-4)
    sched = torch.optim.lr_scheduler.OneCycleLR(opt, max_lr=LR[kind], total_steps=max(1, len(loader) * epochs),
                                                pct_start=0.1)
    job_dir.mkdir(parents=True, exist_ok=True)
    artifact = job_dir / f"{kind}.pt"
    best, started = -1.0, time.time()
    for epoch in range(1, epochs + 1):
        model.train()
        total, n = 0.0, 0
        for images, targets in loader:
            images = [im.to(device) for im in images]
            targets = [{k: v.to(device) for k, v in t.items()} for t in targets]
            loss = sum(model(images, targets).values())
            opt.zero_grad(set_to_none=True)
            loss.backward()
            torch.nn.utils.clip_grad_norm_(params, 10.0)
            opt.step()
            sched.step()
            total, n = total + loss.item(), n + 1
        val = detection_metrics(*_predict(model, val_set, imgsz, device))
        log.info("%s epoch %d/%d: loss %.4f, val mAP50 %.4f, mAP50-95 %.4f", kind, epoch, epochs,
                 total / max(n, 1), val["map50"], val["map50_95"])
        fitness = 0.1 * val["map50"] + 0.9 * val["map50_95"]   # то же правило, что у Ultralytics
        if fitness > best:
            best = fitness
            torch.save(model.state_dict(), artifact)
    log.info("%s: %d эпох за %.0f с, лучшая val fitness %.4f", kind, epochs, time.time() - started, best)
    return str(artifact)


def evaluate(kind: str, weights: str, samples: list[Sample], imgsz: int, pretrained: bool = True) -> dict:
    """pretrained — как при обучении: от него зависит устройство модели (у Faster R-CNN с весами COCO
    слои нормализации замороженные), иначе сохранённые веса не загрузятся."""
    import torch

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model = build_model(kind, pretrained, imgsz)
    model.load_state_dict(torch.load(weights, map_location="cpu", weights_only=True))
    model.to(device)
    # Как у Ultralytics при оценке: низкий порог уверенности, чтобы кривая P–R была полной.
    if kind == "ssdlite":
        model.score_thresh = 0.001
    else:
        model.roi_heads.score_thresh = 0.001
    return detection_metrics(*_predict(model, samples, imgsz, device))
