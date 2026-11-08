"""
CRNN-студент для распознавания номера на кропе: свёрточный экстрактор → BiLSTM → CTC.

CTC учится на строке целиком, без посимвольной разметки — ровно в том виде, в каком метку отдаёт
ансамбль VLM. ~5 млн параметров, обучение — минуты на одной видеокарте.
Алфавит — тот, что остаётся после нормализации TextConsensus (12 букв-двойников и цифры).
"""
from __future__ import annotations

import logging
import os
import time
from pathlib import Path
from typing import Iterator

from .. import vram
from ..common import PLATE_ALPHABET, normalize_text, resolve_frame
from . import Epoch, Sample, Trained, validation_split

log = logging.getLogger("quorum.worker.crnn")

HEIGHT, WIDTH = 32, 128
BLANK = 0
METRIC = "accuracy"
BATCH = int(os.getenv("CRNN_BATCH", "64"))
LR = float(os.getenv("CRNN_LR", "1e-3"))


def _torch():
    import torch
    import torch.nn as nn
    return torch, nn


def build_model(num_classes: int):
    torch, nn = _torch()

    def block(cin, cout, pool):
        layers = [nn.Conv2d(cin, cout, 3, padding=1, bias=False), nn.BatchNorm2d(cout), nn.ReLU(inplace=True)]
        if pool:
            layers.append(nn.MaxPool2d(pool, pool))
        return layers

    class CRNN(nn.Module):
        def __init__(self) -> None:
            super().__init__()
            # По высоте сжимаем сильнее, чем по ширине: ширина несёт последовательность символов.
            self.cnn = nn.Sequential(*block(1, 64, (2, 2)), *block(64, 128, (2, 2)), *block(128, 256, None),
                                     *block(256, 256, (2, 1)), *block(256, 512, None), *block(512, 512, (2, 1)))
            self.rnn = nn.LSTM(512, 256, num_layers=2, bidirectional=True, dropout=0.2)
            self.fc = nn.Linear(512, num_classes)

        def forward(self, x):
            f = self.cnn(x).mean(dim=2)          # (b, c, w)
            out, _ = self.rnn(f.permute(2, 0, 1))  # (t, b, c)
            return self.fc(out)                   # (t, b, classes)

    return CRNN()


def encode(text: str) -> list[int]:
    return [PLATE_ALPHABET.index(c) + 1 for c in normalize_text(text) if c in PLATE_ALPHABET]


def decode(logits) -> list[str]:
    """Жадное CTC-декодирование: сначала схлопываем повторы, потом убираем пустой символ (иначе «AA» → «A»)."""
    out = []
    for seq in logits.argmax(2).permute(1, 0).tolist():
        chars, prev = [], BLANK
        for i in seq:
            if i != prev and i != BLANK:
                chars.append(PLATE_ALPHABET[i - 1])
            prev = i
        out.append("".join(chars))
    return out


def _load_image(path: str, augment: bool):
    import cv2
    import numpy as np

    img = cv2.imread(str(resolve_frame(path)), cv2.IMREAD_GRAYSCALE)
    if img is None:
        return None
    if augment:
        # Имитация уличной съёмки: смаз, перепады яркости, шум, лёгкий поворот.
        rng = np.random
        if rng.rand() < 0.3:
            img = cv2.GaussianBlur(img, (3, 3), 0)
        if rng.rand() < 0.4:
            img = np.clip(img * rng.uniform(0.7, 1.3) + rng.uniform(-25, 25), 0, 255).astype(np.uint8)
        if rng.rand() < 0.2:
            img = np.clip(img + rng.normal(0, 8, img.shape), 0, 255).astype(np.uint8)
        if rng.rand() < 0.3:
            h, w = img.shape
            m = cv2.getRotationMatrix2D((w / 2, h / 2), rng.uniform(-4, 4), 1.0)
            img = cv2.warpAffine(img, m, (w, h), borderMode=cv2.BORDER_REPLICATE)
    img = cv2.resize(img, (WIDTH, HEIGHT), interpolation=cv2.INTER_LINEAR)
    return img.astype("float32") / 127.5 - 1.0


def _batches(samples: list[Sample], augment: bool, shuffle: bool):
    import random
    torch, _ = _torch()
    import numpy as np

    order = list(samples)
    if shuffle:
        random.shuffle(order)
    for i in range(0, len(order), BATCH):
        images, targets, lengths, texts = [], [], [], []
        for s in order[i:i + BATCH]:
            img, code = _load_image(s.path, augment), encode(s.text)
            if img is None or not code:
                continue
            images.append(img)
            targets.extend(code)
            lengths.append(len(code))
            texts.append("".join(PLATE_ALPHABET[c - 1] for c in code))
        if images:
            yield (torch.from_numpy(np.stack(images)).unsqueeze(1), torch.tensor(targets, dtype=torch.long),
                   torch.tensor(lengths, dtype=torch.long), texts)


def _accuracy(model, samples: list[Sample], device) -> float:
    torch, _ = _torch()
    model.eval()
    hit = total = 0
    with torch.inference_mode():
        for images, _, _, texts in _batches(samples, augment=False, shuffle=False):
            for pred, truth in zip(decode(model(images.to(device))), texts):
                hit += pred == truth
                total += 1
    return round(hit / total, 4) if total else 0.0


def _load(artifact: str, device):
    torch, _ = _torch()
    state = torch.load(artifact, map_location=device, weights_only=True)
    model = build_model(len(PLATE_ALPHABET) + 1).to(device)
    model.load_state_dict(state)
    return model


def train(samples: list[Sample], epochs: int, base_artifact: str, artifact_path: Path) -> Iterator[Epoch | Trained]:
    torch, nn = _torch()
    vram.slot.release()
    device = vram.device()
    train_set, val_set = validation_split([s for s in samples if encode(s.text)])
    if not train_set:
        raise ValueError("No readable text labels in the training set")

    # Старт: опубликованная модель камеры → предобученная (CRNN_PRETRAINED) → с нуля.
    # С нуля CTC сначала долго учится выдавать одни пустые символы (~150–200 шагов оптимизатора);
    # на паре сотен меток одного цикла это почти всё обучение, поэтому первый цикл стоит начинать
    # с модели, предобученной один раз на большом наборе (python -m quorum_worker.students.crnn --help).
    start = next((p for p in (base_artifact, os.getenv("CRNN_PRETRAINED", ""))
                  if p.endswith(".pt") and Path(p).is_file()), None)
    if start:
        model = _load(start, device)
        log.info("CRNN fine-tune from %s", start)
    else:
        log.warning("CRNN trains from scratch: set CRNN_PRETRAINED for small cycles")
        model = build_model(len(PLATE_ALPHABET) + 1).to(device)

    steps = max(1, -(-len(train_set) // BATCH)) * epochs
    opt = torch.optim.AdamW(model.parameters(), lr=LR, weight_decay=1e-4)
    sched = torch.optim.lr_scheduler.OneCycleLR(opt, max_lr=LR, total_steps=steps)
    ctc = nn.CTCLoss(blank=BLANK, zero_infinity=True)
    artifact_path.parent.mkdir(parents=True, exist_ok=True)

    best, started = -1.0, time.time()
    for epoch in range(1, epochs + 1):
        model.train()
        total, n = 0.0, 0
        for images, targets, lengths, _ in _batches(train_set, augment=True, shuffle=True):
            log_probs = model(images.to(device)).log_softmax(2)
            input_lengths = torch.full((images.size(0),), log_probs.size(0), dtype=torch.long)
            loss = ctc(log_probs.float().cpu(), targets, input_lengths, lengths)
            opt.zero_grad(set_to_none=True)
            loss.backward()
            # Рекуррентная часть склонна к взрыву градиента — на зашумлённых метках тем более.
            torch.nn.utils.clip_grad_norm_(model.parameters(), 5.0)
            opt.step()
            if sched.last_epoch < steps - 1:
                sched.step()
            total, n = total + loss.item(), n + 1
        acc = _accuracy(model, val_set, device)
        if acc > best:
            best = acc
            torch.save(model.state_dict(), artifact_path)
        yield Epoch(epoch, epochs, round(total / max(n, 1), 4))

    log.info("CRNN trained in %.0fs, best val accuracy %.3f", time.time() - started, best)
    yield Trained(str(artifact_path), METRIC, best)


def evaluate_artifact(artifact: str, samples: list[Sample]) -> float:
    return _accuracy(_load(artifact, vram.device()), samples, vram.device())


def main() -> None:
    """Предобучение на размеченных кропах (текст в имени файла) — один раз, затем CRNN_PRETRAINED."""
    import argparse
    import logging

    ap = argparse.ArgumentParser(description="Pretrain CRNN on plate crops with text in file names")
    ap.add_argument("--data", type=Path, required=True, help="каталог с кропами, например A123BC77___120___40.png")
    ap.add_argument("--out", type=Path, required=True, help="куда сохранить веса, например /models/crnn-ru.pt")
    ap.add_argument("--epochs", type=int, default=30)
    ap.add_argument("--limit", type=int, help="взять не больше N кропов")
    args = ap.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")

    import re
    images = sorted(p for p in args.data.rglob("*") if p.suffix.lower() in {".png", ".jpg", ".jpeg", ".bmp"})
    samples = []
    for p in images:
        text = normalize_text(re.sub(r"[^0-9A-Za-zА-Яа-я]", "", p.stem.split("___")[0]))
        if sum(c.isdigit() for c in text) >= 3:
            samples.append(Sample(str(p.relative_to(args.data)), str(p.resolve()), [], text))
    if args.limit:
        samples = samples[:args.limit]
    log.info("Pretraining on %d crops from %s", len(samples), args.data)
    for ev in train(samples, args.epochs, "", args.out):
        log.info("%s", ev)


if __name__ == "__main__":
    main()
