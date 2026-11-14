"""
Загрузчики размеченных наборов из курсовых работ.

coco  — набор для детекции (курсовая 2 курса): каталог страны с images/ и labels/output_coco.json,
        либо корень со многими такими каталогами. Метка домена — атрибут country (или имя каталога).
ocr   — кропы номеров, текст зашит в имя файла: A123BC77___120___40.png (курсовая 2 курса)
        или просто A123BC77.png. Метка домена — имя подкаталога (например, день съёмки).
"""
from __future__ import annotations

import json
import logging
import random
import re
from dataclasses import dataclass, field
from pathlib import Path

from quorum_worker.common import Box, normalize_text

IMAGES = {".jpg", ".jpeg", ".png", ".bmp", ".webp"}

log = logging.getLogger("experiments.datasets")


def is_junk(path: Path) -> bool:
    """
    Служебные файлы, которые выглядят как данные. Архив, распакованный на macOS или собранный там,
    содержит «._имя.json» и «._имя.png» (AppleDouble: метаданные Finder в двоичном виде) и каталог
    __MACOSX — расширение у них настоящее, а содержимое нет.
    """
    return any(part.startswith("._") or part in ("__MACOSX", ".DS_Store") for part in path.parts)


def _read_coco(path: Path) -> dict | None:
    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
    except (UnicodeDecodeError, json.JSONDecodeError) as e:
        log.warning("Skip %s: not a readable JSON (%s)", path, e)
        return None
    if not isinstance(data, dict) or "images" not in data or "annotations" not in data:
        log.info("Skip %s: not COCO annotations", path)
        return None
    return data


@dataclass
class Item:
    frame_id: str
    path: str                  # абсолютный путь
    boxes: list[Box] = field(default_factory=list)
    text: str = ""
    domain: str = ""

    def to_json(self) -> dict:
        return {"frame_id": self.frame_id, "path": self.path, "text": self.text, "domain": self.domain,
                "boxes": [[b.x, b.y, b.w, b.h] for b in self.boxes]}

    @staticmethod
    def from_json(d: dict) -> "Item":
        return Item(d["frame_id"], d["path"], [Box(*b) for b in d["boxes"]], d["text"], d["domain"])


def load_coco(root: Path) -> list[Item]:
    # Курсовая: <страна>/labels/output_coco.json. Экспорт Roboflow: <split>/_annotations.coco.json рядом с картинками.
    files = [root / "labels" / "output_coco.json"] if (root / "labels" / "output_coco.json").is_file() \
        else sorted(root.glob("*/labels/output_coco.json")) or sorted(root.glob("**/*.json"))
    files = [f for f in files if not is_junk(f.relative_to(root))]
    items: list[Item] = []
    for ann_file in files:
        coco = _read_coco(ann_file)
        if coco is None:
            continue
        log.info("Annotations: %s", ann_file.relative_to(root))
        images_dir = ann_file.parent.parent / "images"
        if not images_dir.is_dir():
            images_dir = ann_file.parent
        by_image: dict = {}
        countries: dict = {}
        for a in coco.get("annotations", []):
            by_image.setdefault(a["image_id"], []).append(a["bbox"])
            countries.setdefault(a["image_id"], (a.get("attributes") or {}).get("country"))
        for img in coco.get("images", []):
            path = images_dir / img["file_name"]
            # В курсовой из каталогов удаляли дубликаты, а в json записи остались — такие пропускаем.
            if not path.is_file():
                continue
            w, h = img.get("width"), img.get("height")
            if not w or not h:
                from PIL import Image
                with Image.open(path) as im:
                    w, h = im.size
            boxes = []
            for x, y, bw, bh in by_image.get(img["id"], []):
                x0, y0 = max(0.0, x / w), max(0.0, y / h)
                bw, bh = min(bw / w, 1 - x0), min(bh / h, 1 - y0)
                if bw > 0 and bh > 0:
                    boxes.append(Box(x0, y0, bw, bh))
            domain = countries.get(img["id"]) or ann_file.parent.parent.name
            # Путь относительно корня — уникален и между странами, и между train/valid/test.
            items.append(Item(str(path.relative_to(root)), str(path.resolve()), boxes, "", domain))
    if not items:
        log.warning("No labelled images found under %s (checked %d annotation files)", root, len(files))
    return items


def text_from_name(name: str) -> str:
    stem = Path(name).stem.split("___")[0]
    return normalize_text(re.sub(r"[^0-9A-Za-zА-Яа-я]", "", stem))


def load_ocr(root: Path) -> list[Item]:
    items = []
    for path in sorted(p for p in root.rglob("*") if p.suffix.lower() in IMAGES and not is_junk(p.relative_to(root))):
        text = text_from_name(path.name)
        if sum(c.isdigit() for c in text) < 3:   # имя файла не похоже на номер — эталона нет
            continue
        rel = path.relative_to(root)
        items.append(Item(str(rel), str(path.resolve()), [], text, rel.parts[0] if len(rel.parts) > 1 else root.name))
    return items


def load(kind: str, root: Path) -> list[Item]:
    if kind == "coco":
        return load_coco(root)
    if kind == "ocr":
        return load_ocr(root)
    raise ValueError(f"Unknown dataset format {kind!r}: expected coco or ocr")


def sample(items: list[Item], limit: int | None, seed: int = 0, per_domain: bool = False) -> list[Item]:
    """Случайная подвыборка. per_domain — поровну из каждого домена, чтобы мелкие страны не потерялись."""
    if not limit or limit >= len(items):
        return items
    rnd = random.Random(seed)
    if not per_domain:
        return sorted(rnd.sample(items, limit), key=lambda i: i.frame_id)
    groups: dict[str, list[Item]] = {}
    for it in items:
        groups.setdefault(it.domain, []).append(it)
    quota = max(1, limit // len(groups))
    picked = [x for g in groups.values() for x in rnd.sample(g, min(quota, len(g)))]
    return sorted(picked, key=lambda i: i.frame_id)
