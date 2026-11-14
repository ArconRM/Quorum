"""
Посмотреть глазами, что нарисовали учителя: эталон и боксы каждого учителя поверх кадров.

  python -m experiments.show --out runs/detect_v1                          # 24 случайных кадра
  python -m experiments.show --out runs/detect_v1 --errors grounding-dino  # где учитель ошибся
  python -m experiments.show --out runs/detect_v1 --frames 48 --min-score owlv2=0.25

Картинки — <out>/show/*.jpg, по 12 кадров на лист. Зелёный — эталон, остальные цвета — учителя,
у бокса подписана уверенность. Цифры в таблице говорят «сколько ошибок», а здесь видно, какие:
бокс на всей машине, на соседней табличке, сдвинут или его нет вовсе.
"""
from __future__ import annotations

import argparse
import random
from pathlib import Path

from .evaluate import apply_min_scores, boxes_of, load_predictions, match
from .run import load_manifest, resolve_out

PALETTE = ["#e45756", "#4c78a8", "#f58518", "#b279a2", "#72b7b2", "#9d755d"]
TRUTH = "#2ca02c"
TILE = 420
PER_SHEET = 12
COLS = 4


def draw(item, preds, teachers, colors):
    from PIL import Image, ImageDraw

    with Image.open(item.path) as src:
        img = src.convert("RGB")
    scale = TILE / max(img.size)
    img = img.resize((max(1, round(img.width * scale)), max(1, round(img.height * scale))))
    d = ImageDraw.Draw(img)
    w, h = img.size

    def rect(b, color, width, label=None):
        x0, y0, x1, y1 = b.x * w, b.y * h, (b.x + b.w) * w, (b.y + b.h) * h
        d.rectangle([x0, y0, x1, y1], outline=color, width=width)
        if label:
            d.text((x0 + 2, max(0, y0 - 11)), label, fill=color)

    for b in item.boxes:
        rect(b, TRUTH, 3)
    for t in teachers:
        for b in boxes_of(preds[t].get(item.frame_id)):
            rect(b, colors[t], 2, f"{b.score:.2f}")
    return img


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--frames", type=int, default=24)
    ap.add_argument("--errors", help="показать только кадры, где этот учитель ошибся")
    ap.add_argument("--teachers", default="", help="рисовать только этих учителей")
    ap.add_argument("--min-score", default="")
    ap.add_argument("--seed", type=int, default=0)
    args = ap.parse_args()

    from PIL import Image, ImageDraw

    out = resolve_out(args.out)
    items = load_manifest(out)
    preds = load_predictions(out)
    apply_min_scores(preds, {k.strip(): float(v) for k, v in (p.split("=") for p in args.min_score.split(",") if "=" in p)})
    teachers = [t.strip() for t in args.teachers.split(",") if t.strip()] or sorted(preds)
    colors = {t: PALETTE[i % len(PALETTE)] for i, t in enumerate(teachers)}

    if args.errors:
        if args.errors not in preds:
            raise SystemExit(f"--errors: нет учителя {args.errors!r}; есть {sorted(preds)}")
        items = [i for i in items if match(boxes_of(preds[args.errors].get(i.frame_id)), i.boxes)[1:] != (0, 0)]
        print(f"{args.errors} ошибся на {len(items)} кадрах")
    random.Random(args.seed).shuffle(items)
    items = items[:args.frames]

    target = out / "show"
    target.mkdir(exist_ok=True)
    for sheet, start in enumerate(range(0, len(items), PER_SHEET), 1):
        chunk = items[start:start + PER_SHEET]
        rows = (len(chunk) + COLS - 1) // COLS
        canvas = Image.new("RGB", (COLS * TILE, rows * TILE + 24), "white")
        d = ImageDraw.Draw(canvas)
        x = 6
        # Подписи латиницей: встроенный шрифт PIL не знает кириллицы, а TTF в образе может не быть.
        for name, color in [("ground truth", TRUTH)] + [(t, colors[t]) for t in teachers]:
            d.rectangle([x, 6, x + 12, 18], fill=color)
            d.text((x + 16, 6), name, fill="black")
            x += 24 + 7 * len(name)
        for k, item in enumerate(chunk):
            tile = draw(item, preds, teachers, colors)
            canvas.paste(tile, ((k % COLS) * TILE, 24 + (k // COLS) * TILE))
        name = f"{args.errors or 'random'}_{sheet:02d}.jpg"
        canvas.save(target / name, quality=90)
        print(target / name)
    if not items:
        print("Нет кадров для показа")


if __name__ == "__main__":
    main()
