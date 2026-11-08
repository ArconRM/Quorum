"""
Общие типы и функции воркера: бокс в нормированных координатах, нормализация текста номера,
пути к кадрам. Без тяжёлых зависимостей — импортируется и в заглушке, и в экспериментах.
"""
from __future__ import annotations

import os
import re
from dataclasses import dataclass
from pathlib import Path

FRAMES_ROOT = Path(os.getenv("FRAMES_ROOT", "/data/frames"))


@dataclass(frozen=True)
class Box:
    """Как в worker.proto: левый верхний угол, ширина, высота — всё в долях кадра [0, 1]."""
    x: float
    y: float
    w: float
    h: float
    score: float = 1.0

    @property
    def area(self) -> float:
        return self.w * self.h

    def iou(self, other: "Box") -> float:
        ix = max(0.0, min(self.x + self.w, other.x + other.w) - max(self.x, other.x))
        iy = max(0.0, min(self.y + self.h, other.y + other.h) - max(self.y, other.y))
        inter = ix * iy
        union = self.area + other.area - inter
        return 0.0 if union <= 0 else inter / union


def box_from_xyxy(x1: float, y1: float, x2: float, y2: float, width: int, height: int, score: float) -> Box | None:
    """Пиксельные углы → нормированный бокс. Вылезающее за кадр обрезается, вырожденное отбрасывается."""
    if width <= 0 or height <= 0:
        return None
    x1, x2 = sorted((min(max(x1, 0.0), width), min(max(x2, 0.0), width)))
    y1, y2 = sorted((min(max(y1, 0.0), height), min(max(y2, 0.0), height)))
    w, h = (x2 - x1) / width, (y2 - y1) / height
    if w <= 1e-4 or h <= 1e-4:
        return None
    return Box(x1 / width, y1 / height, w, h, float(min(max(score, 0.0), 1.0)))


def resolve_frame(path: str) -> Path:
    """Путь из БД относительный («{cameraId}/{frameId}.jpg»); абсолютный — для экспериментов вне платформы."""
    p = Path(path)
    return p if p.is_absolute() else FRAMES_ROOT / p


# ---------------- текст номера ----------------

# Та же таблица, что в Labeling.Domain/Consensus/TextConsensus.cs: кириллица-двойник → латиница.
HOMOGLYPHS = str.maketrans("АВЕКМНОРСТУХ", "ABEKMHOPCTYX")
SEPARATORS = set(" \t\r\n-_|.,")
PLATE_ALPHABET = "ABEKMHOPCTYX0123456789"
RU_PLATE = re.compile(r"[ABEKMHOPCTYX]\d{3}[ABEKMHOPCTYX]{2}\d{2,3}")


def normalize_text(text: str | None) -> str:
    """Точная копия TextConsensus.Normalize из C#: верхний регистр, без разделителей, двойники → латиница."""
    if not text or not text.strip():
        return ""
    return "".join(c for c in text.upper() if c not in SEPARATORS).translate(HOMOGLYPHS)


def clean_vlm_answer(raw: str | None, require_digits: int = 3) -> str:
    """
    Ответ VLM → строка для голосования или "" («не читается»).

    Модели добавляют пояснения, кавычки, служебные теги рассуждений. Главная ловушка: ответ вроде
    «НЕТ НОМЕРА» после нормализации превращается в правдоподобное «HETHOMEPA» (все буквы — двойники)
    и молча попадает в разметку. В номере всегда есть цифры, поэтому ответ без них считается отказом.
    """
    if not raw:
        return ""
    text = re.sub(r"<think>.*?</think>", " ", raw, flags=re.S | re.I)
    text = re.sub(r"<[^>]+>", " ", text)
    squashed = normalize_text(text)

    strict = RU_PLATE.search(squashed)
    if strict:
        return strict.group(0)

    # Не российский формат: берём самый длинный кусок из букв и цифр в первой непустой строке.
    line = next((l for l in text.splitlines() if l.strip()), "")
    candidates = re.findall(r"[0-9A-ZА-Я]+", normalize_text(line))
    best = max(candidates, key=len, default="")
    if sum(c.isdigit() for c in best) < require_digits or len(best) > 16:
        return ""
    return best
