"""
Детерминированная имитация моделей. Никакого ML — только правдоподобные данные:

* у каждого кадра есть «истинная» разметка, выведенная из его id (1–2 номерные пластины или текст номера);
* у каждого учителя есть «качество», выведенное из его имени: он пропускает объекты, сдвигает рамки,
  находит лишнее, ошибается в символах, а VLM иногда пишут кириллицей;
* каждый 7-й кадр — «сложный» (ночь, блик): там все учителя ошибаются чаще, и консенсус расходится.

Одинаковый вход всегда даёт одинаковый ответ, поэтому повторный запуск после сбоя воспроизводим.
"""
import hashlib
import math
import random
from dataclasses import dataclass

PLATE_LETTERS = "ABEKMHOPCTYX"           # буквы, общие для кириллицы и латиницы
CYRILLIC = dict(zip("ABEKMHOPCTYX", "АВЕКМНОРСТУХ"))


def rng(*parts: str) -> random.Random:
    digest = hashlib.sha256("|".join(parts).encode()).digest()
    return random.Random(int.from_bytes(digest[:8], "big"))


@dataclass
class Box:
    x: float
    y: float
    w: float
    h: float
    score: float = 1.0


def is_hard(frame_id: str) -> bool:
    return rng("hard", frame_id).randrange(7) == 0


def truth_boxes(frame_id: str) -> list[Box]:
    r = rng("truth-boxes", frame_id)
    boxes = []
    for _ in range(1 + r.randrange(2)):
        w, h = r.uniform(0.08, 0.2), r.uniform(0.03, 0.08)
        boxes.append(Box(r.uniform(0, 1 - w), r.uniform(0, 1 - h), w, h))
    return boxes


def truth_text(frame_id: str) -> str:
    r = rng("truth-text", frame_id)
    letters = lambda n: "".join(r.choice(PLATE_LETTERS) for _ in range(n))
    digits = lambda n: "".join(r.choice("0123456789") for _ in range(n))
    return f"{letters(1)}{digits(3)}{letters(2)}{digits(2 + r.randrange(2))}"


def teacher_skill(teacher: str, frame_id: str) -> float:
    base = 0.78 + 0.17 * rng("skill", teacher).random()           # 0.78 … 0.95
    return base * (0.7 if is_hard(frame_id) else 1.0)


def clamp_box(x: float, y: float, w: float, h: float, score: float) -> Box | None:
    x, y = min(max(x, 0.0), 1.0), min(max(y, 0.0), 1.0)
    w, h = min(w, 1 - x), min(h, 1 - y)
    return Box(x, y, w, h, score) if w > 0.005 and h > 0.005 else None


def predict_boxes(teacher: str, frame_id: str) -> tuple[list[Box], float]:
    skill = teacher_skill(teacher, frame_id)
    r = rng("boxes", teacher, frame_id)
    noise = (1 - skill) * 0.08
    out = []
    for t in truth_boxes(frame_id):
        if r.random() > skill:              # пропустил объект
            continue
        b = clamp_box(t.x + r.gauss(0, noise * t.w), t.y + r.gauss(0, noise * t.h),
                      t.w * (1 + r.gauss(0, noise)), t.h * (1 + r.gauss(0, noise)),
                      round(min(0.99, skill * r.uniform(0.85, 1.05)), 3))
        if b:
            out.append(b)
    if r.random() < (1 - skill) * 0.6:      # ложное срабатывание
        w, h = r.uniform(0.05, 0.15), r.uniform(0.03, 0.06)
        out.append(Box(r.uniform(0, 1 - w), r.uniform(0, 1 - h), w, h, round(r.uniform(0.3, 0.6), 3)))
    confidence = sum(b.score for b in out) / len(out) if out else 0.0
    return out, round(confidence, 3)


def predict_text(teacher: str, kind: str, frame_id: str) -> tuple[str, float]:
    skill = teacher_skill(teacher, frame_id)
    r = rng("text", teacher, frame_id)
    if r.random() > skill + 0.08:           # не смог прочитать
        return "", 0.0
    chars = list(truth_text(frame_id))
    if r.random() > skill:                   # ошибка в одном символе
        i = r.randrange(len(chars))
        pool = "0123456789" if chars[i].isdigit() else PLATE_LETTERS
        chars[i] = r.choice([c for c in pool if c != chars[i]])
    text = "".join(chars)
    if kind == "Vlm" and r.random() < 0.3:   # VLM иногда отвечает кириллицей — консенсус это нормализует
        text = "".join(CYRILLIC.get(c, c) for c in text)
    if r.random() < 0.2:
        text = f"{text[:4]} {text[4:]}"       # и с пробелом
    return text, round(skill * r.uniform(0.9, 1.0), 3)


# ---------------- обучение и оценка ----------------

BASE_METRIC = {"Detection": ("mAP50", 0.55), "Ocr": ("accuracy", 0.60)}


def trained_metric(task: str, total_samples: int, seed: str) -> tuple[str, float]:
    """Качество растёт с объёмом накопленных данных и насыщается — как у настоящей адаптации."""
    name, base = BASE_METRIC.get(task, ("score", 0.5))
    value = base + 0.35 * (1 - math.exp(-total_samples / 400)) + rng("train", seed).gauss(0, 0.005)
    return name, round(min(value, 0.99), 4)


def loss_curve(epoch: int, epochs: int, seed: str) -> float:
    return round(1.2 * math.exp(-2.5 * epoch / epochs) + 0.05 + abs(rng("loss", seed, str(epoch)).gauss(0, 0.02)), 4)


def evaluate(metric: float, holdout_ids: list[str], artifact: str) -> float:
    """Оценка на отложенной выборке: метрика модели плюс небольшой шум, зависящий от выборки."""
    noise = rng("eval", artifact, *sorted(holdout_ids)).gauss(0, 0.01)
    return round(min(max(metric + noise, 0.0), 0.99), 4)
