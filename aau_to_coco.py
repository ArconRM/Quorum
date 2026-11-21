#!/usr/bin/env python3
"""
Готовит AAU RainSnow (Kaggle) к экспериментам Quorum: по набору на каждый класс, по домену на каждую
видеопоследовательность (перекрёсток + номер записи).

Консенсус Quorum не различает классы, поэтому эксперимент идёт по одному классу за раз: для каждого
класса пишется свой набор datasets/<name>-<класс>/, где у каждого кадра размечены только объекты
этого класса, а кадры без них остаются пустыми (это честные отрицательные примеры, а не пропуски).
Картинки лежат один раз в datasets/<name>/images/, наборы классов ссылаются на них относительными
ссылками — внутри контейнера (/datasets) они тоже работают.

    python3 aau_to_coco.py --json ~/Downloads/aau-rainsnow/aauRainSnow-rgb.json \
        --images ~/Downloads/aau-rainsnow --dst datasets --classes person,car,vehicle=car+truck+bus

Сначала можно запустить без --classes: скрипт только покажет классы и последовательности.
Исходные данные только читаются.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
from collections import Counter, defaultdict
from pathlib import Path


def sequence_of(file_name: str) -> str:
    """Домен — каталог кадра: «Egensevej/Egensevej-1/cam1-00012.png» → «Egensevej-1»."""
    parts = Path(file_name).parts
    return parts[-2] if len(parts) >= 2 else "all"


def parse_classes(text: str, available: dict[str, int]) -> dict[str, set[int]]:
    """«person,vehicle=car+truck» → {"person": {id}, "vehicle": {ids}}; имена без учёта регистра."""
    by_name = {n.lower(): i for n, i in available.items()}
    out = {}
    for part in (p.strip() for p in text.split(",") if p.strip()):
        name, _, members = part.partition("=")
        members = members or name
        ids = set()
        for m in members.split("+"):
            if m.strip().lower() not in by_name:
                raise SystemExit(f"Нет класса {m!r}; есть: {', '.join(sorted(available))}")
            ids.add(by_name[m.strip().lower()])
        out[name.strip().lower()] = ids
    return out


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--json", type=Path, required=True, help="файл разметки COCO (для RGB-камеры)")
    ap.add_argument("--images", type=Path, required=True, help="каталог, от которого отсчитываются file_name")
    ap.add_argument("--dst", type=Path, default=Path("datasets"), help="папка datasets репозитория")
    ap.add_argument("--name", default="aau", help="префикс наборов")
    ap.add_argument("--classes", default="", help="классы через запятую; группа — имя=класс+класс")
    ap.add_argument("--skip-crowd", action="store_true", help="не брать объекты с iscrowd=1 (толпа, области «не учитывать»)")
    args = ap.parse_args()
    src, dst = args.images.expanduser(), args.dst.expanduser()

    coco = json.loads(args.json.expanduser().read_text(encoding="utf-8"))
    categories = {c["name"]: c["id"] for c in coco.get("categories", [])}
    images = {img["id"]: img for img in coco.get("images", [])}
    anns = defaultdict(list)
    for a in coco.get("annotations", []):
        anns[a["image_id"]].append(a)

    per_class = Counter(a["category_id"] for a in coco.get("annotations", []))
    print("Классы (объектов):")
    for name, cid in sorted(categories.items(), key=lambda x: -per_class[x[1]]):
        print(f"  {name}: {per_class[cid]}")
    seqs = Counter(sequence_of(img["file_name"]) for img in images.values())
    print(f"Кадров: {len(images)}, последовательностей: {len(seqs)}")
    for s, n in sorted(seqs.items()):
        print(f"  {s}: {n}")
    missing = [img["file_name"] for img in images.values() if not (src / img["file_name"]).is_file()]
    if missing:
        print(f"! не найдено картинок: {len(missing)}, например {missing[:3]}; проверь --images")
    if not args.classes:
        print("Это был обзор. Задай --classes, чтобы собрать наборы.")
        return

    wanted = parse_classes(args.classes, categories)
    pool = dst / args.name / "images"
    for img in images.values():
        source = src / img["file_name"]
        if not source.is_file():
            continue
        target = pool / sequence_of(img["file_name"]) / Path(img["file_name"]).name
        if not target.is_file():
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)

    for cls, ids in wanted.items():
        root = dst / f"{args.name}-{cls}"
        if root.exists():
            shutil.rmtree(root)
        total = Counter()
        for seq in sorted(seqs):
            seq_dir = root / seq
            (seq_dir / "labels").mkdir(parents=True)
            # Относительная ссылка на общую папку кадров: работает и на хосте, и в контейнере.
            os.symlink(os.path.relpath(pool / seq, seq_dir), seq_dir / "images")
            out = {"categories": [{"id": 1, "name": cls}], "images": [], "annotations": []}
            for img in sorted((i for i in images.values() if sequence_of(i["file_name"]) == seq), key=lambda i: i["file_name"]):
                if not (src / img["file_name"]).is_file():
                    continue
                entry = {"id": len(out["images"]) + 1, "file_name": Path(img["file_name"]).name}
                if img.get("width") and img.get("height"):
                    entry |= {"width": img["width"], "height": img["height"]}
                out["images"].append(entry)
                for a in anns[img["id"]]:
                    if a["category_id"] in ids and not (args.skip_crowd and a.get("iscrowd")):
                        out["annotations"].append({"id": len(out["annotations"]) + 1, "image_id": entry["id"],
                                                   "category_id": 1, "bbox": a["bbox"]})
            (seq_dir / "labels" / "output_coco.json").write_text(json.dumps(out), encoding="utf-8")
            with_obj = len({a["image_id"] for a in out["annotations"]})
            total.update(frames=len(out["images"]), with_objects=with_obj, boxes=len(out["annotations"]))
        print(f"{root}: кадров {total['frames']}, с объектами {total['with_objects']}, рамок {total['boxes']}")


if __name__ == "__main__":
    main()
