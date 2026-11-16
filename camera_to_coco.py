#!/usr/bin/env python3
"""
Собирает набор для детекции из данных курсовой 3 курса (папка data_2) в формате,
который читает experiments.run (--format coco):

    <dst>/<ГГГГ-ММ-ДД>/images/frame_*.jpg
    <dst>/<ГГГГ-ММ-ДД>/labels/output_coco.json

Эталон — ответы YOLOv8l, вычищенные вручную: в json лежат все срабатывания модели, а кропы ложных
срабатываний были удалены руками. Поэтому рамка считается эталонной, только если её кроп до сих пор
лежит в папке crops/yolov8l/<день>/.

Кроме кадров с номерами берётся доля пустых кадров того же дня (отрицательные примеры):
сначала кадры с удалённым ложным срабатыванием (самые трудные), затем случайные.

Исходные данные только читаются. Пример:

    python3 camera_to_coco.py --src /run/media/artemiy/EXTERNAL_USB/FUCK/data_2 \
        --dst ~/Documents/GitHub/quorum/datasets/camera
"""
from __future__ import annotations

import argparse
import json
import random
import shutil
from pathlib import Path

YEAR = 2025  # съёмка: ноябрь–декабрь 2025


def day_name(folder: str) -> str:
    """«06_12» → «2025-12-06»: имена сортируются по времени, это же метка домена в отчётах."""
    day, month = folder.split("_")
    return f"{YEAR}-{int(month):02d}-{int(day):02d}"


def is_junk(path: Path) -> bool:
    return path.name.startswith("._") or "__MACOSX" in path.parts


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--src", type=Path, required=True, help="папка data_2 (внутри raw/ и crops/yolov8l/)")
    ap.add_argument("--dst", type=Path, required=True, help="куда положить набор, например datasets/camera")
    ap.add_argument("--reference", default="yolov8l", help="модель-эталон в crops/")
    ap.add_argument("--empty-ratio", type=float, default=0.2, help="пустых кадров на один кадр с номером")
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--dry-run", action="store_true", help="только посчитать, ничего не копировать")
    args = ap.parse_args()
    src, dst = args.src.expanduser(), args.dst.expanduser()

    crops_root = src / "crops" / args.reference
    if not crops_root.is_dir():
        raise SystemExit(f"Нет папки {crops_root}")

    print("Индексирую кадры в raw/ ...")
    frames: dict[str, Path] = {}
    by_folder: dict[Path, list[Path]] = {}
    for p in (src / "raw").rglob("*.jpg"):
        if is_junk(p):
            continue
        frames.setdefault(p.name, p)
        by_folder.setdefault(p.parent, []).append(p)
    print(f"  кадров: {len(frames)}")

    rnd = random.Random(args.seed)
    total = {"frames": 0, "boxes": 0, "empty": 0, "removed": 0, "lost": 0}
    for day_dir in sorted(d for d in crops_root.iterdir() if d.is_dir()):
        results = [f for f in sorted(day_dir.glob("*.json")) if not is_junk(f)]
        if not results:
            print(f"! {day_dir.name}: нет json с результатами, пропускаю")
            continue
        entries = json.loads(results[0].read_text(encoding="utf-8"))

        boxes: dict[str, list[list[float]]] = {}   # имя кадра → рамки [x, y, w, h] в пикселях
        rejected: set[str] = set()                 # кадры, где срабатывание удалено при чистке
        lost = 0
        for e in entries:
            name = Path(e["frame"]).name
            if name not in frames:
                lost += 1
                continue
            if (day_dir / e["crop_filename"]).is_file():
                c = e["coordinates"]
                boxes.setdefault(name, []).append([c["x1"], c["y1"], c["x2"] - c["x1"], c["y2"] - c["y1"]])
            else:
                rejected.add(name)
        rejected -= set(boxes)

        # Пустые кадры того же дня: сначала с удалённым ложным срабатыванием, затем случайные.
        folder = frames[next(iter(boxes))].parent if boxes else None
        n_empty = round(len(boxes) * args.empty_ratio)
        hard = sorted(rejected)
        rnd.shuffle(hard)
        empty = hard[:n_empty]
        if folder is not None and len(empty) < n_empty:
            taken = set(boxes) | set(empty)
            rest = sorted(p.name for p in by_folder.get(folder, []) if p.name not in taken)
            rnd.shuffle(rest)
            empty += rest[:n_empty - len(empty)]

        n_boxes = sum(len(b) for b in boxes.values())
        n_removed = len(entries) - n_boxes - lost
        name = day_name(day_dir.name)
        print(f"{name}: кадров с номерами {len(boxes)}, рамок {n_boxes}, пустых кадров {len(empty)}; "
              f"удалено при чистке {n_removed}, кадр не найден {lost}")
        for k, v in (("frames", len(boxes)), ("boxes", n_boxes), ("empty", len(empty)), ("removed", n_removed), ("lost", lost)):
            total[k] += v
        if args.dry_run:
            continue

        images_dir, labels_dir = dst / name / "images", dst / name / "labels"
        images_dir.mkdir(parents=True, exist_ok=True)
        labels_dir.mkdir(parents=True, exist_ok=True)
        coco = {"categories": [{"id": 1, "name": "license_plate"}], "images": [], "annotations": []}
        for image_id, frame in enumerate(sorted(boxes) + sorted(empty), 1):
            target = images_dir / frame
            if not target.is_file():
                shutil.copy2(frames[frame], target)
            coco["images"].append({"id": image_id, "file_name": frame})   # размер кадра загрузчик прочитает сам
            for b in boxes.get(frame, []):
                coco["annotations"].append({"id": len(coco["annotations"]) + 1, "image_id": image_id,
                                            "category_id": 1, "bbox": b})
        (labels_dir / "output_coco.json").write_text(json.dumps(coco, ensure_ascii=False), encoding="utf-8")

    print(f"Итого: кадров с номерами {total['frames']}, рамок {total['boxes']}, пустых кадров {total['empty']}; "
          f"удалено при чистке {total['removed']}, кадр не найден {total['lost']}")
    if args.dry_run:
        print("Это был пробный запуск (--dry-run): ничего не скопировано.")
    else:
        print(f"Набор: {dst}")


if __name__ == "__main__":
    main()
