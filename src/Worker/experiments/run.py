"""
Прогон учителей по размеченному набору с кэшем на диске.

  python -m experiments.run --task detection --data /data/datasets/detection/Германия --format coco \\
      --teachers grounding-dino-base,yolo-world-l,owlv2-base --prompt "license plate" --limit 500 \\
      --out runs/detect_de

Учителя идут строго по очереди (16 ГБ видеопамяти). Кэш — <out>/predictions/<учитель>.jsonl, по строке на кадр;
повторный запуск продолжает с места остановки, а добавить учителя можно, не перезапуская старых.
Выборка фиксируется в <out>/manifest.jsonl при первом запуске — все учителя видят одни и те же кадры.
"""
from __future__ import annotations

import argparse
import json
import logging
import os
import time
from pathlib import Path

from quorum_worker.teachers import resolve

from .datasets import Item, load, sample

log = logging.getLogger("experiments.run")


def kind_for(task: str, teacher: str) -> str:
    if task == "ocr":
        return "Vlm"
    return "OpenVocabDetector" if teacher.lower().startswith(("grounding", "mm-grounding", "mm_grounding", "mmgrounding", "llmdet", "yolo-world", "yoloworld", "owl")) else "Detector"


def resolve_out(path: Path) -> Path:
    """
    Относительный --out считается от EXPERIMENTS_ROOT (в контейнере — /app, куда смонтирован ./runs),
    а не от текущего каталога: рабочий каталог образа — кэш весов, и результаты оказались бы
    внутри docker-тома, а не в папке runs на хосте.
    """
    root = os.getenv("EXPERIMENTS_ROOT")
    return path if path.is_absolute() or not root else Path(root) / path


def load_manifest(out: Path) -> list[Item]:
    return [Item.from_json(json.loads(l)) for l in (out / "manifest.jsonl").read_text(encoding="utf-8").splitlines() if l]


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--task", choices=["detection", "ocr"], required=True)
    ap.add_argument("--data", type=Path, required=True, help="корень набора")
    ap.add_argument("--format", choices=["coco", "ocr"], help="по умолчанию: coco для detection, ocr для ocr")
    ap.add_argument("--teachers", required=True, help="через запятую, имена как в реестре Labeling")
    ap.add_argument("--prompt", default="license plate")
    ap.add_argument("--classes", default="", help="coco: имена классов эталона через запятую; остальные рамки и кадры без этих классов отбрасываются")
    ap.add_argument("--limit", type=int, help="размер случайной подвыборки")
    ap.add_argument("--per-domain", action="store_true", help="поровну кадров из каждого домена")
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--out", type=Path, required=True)
    args = ap.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    args.out = resolve_out(args.out)
    log.info("Results: %s", args.out)

    args.out.mkdir(parents=True, exist_ok=True)
    manifest = args.out / "manifest.jsonl"
    if manifest.is_file():
        items = load_manifest(args.out)
        log.info("Manifest exists: %d frames (выборка не меняется между запусками)", len(items))
    else:
        classes = [c.strip() for c in args.classes.split(",") if c.strip()]
        items = sample(load(args.format or ("coco" if args.task == "detection" else "ocr"), args.data, classes or None),
                       args.limit, args.seed, args.per_domain)
        if not items:
            raise SystemExit(f"No labelled frames found in {args.data}")
        manifest.write_text("".join(json.dumps(i.to_json(), ensure_ascii=False) + "\n" for i in items), encoding="utf-8")
        (args.out / "config.json").write_text(json.dumps(vars(args) | {"data": str(args.data), "out": str(args.out)},
                                                         ensure_ascii=False, indent=2), encoding="utf-8")
        log.info("Sampled %d frames from %s", len(items), args.data)

    pred_dir = args.out / "predictions"
    pred_dir.mkdir(exist_ok=True)
    for name in [t.strip() for t in args.teachers.split(",") if t.strip()]:
        cache = pred_dir / f"{name}.jsonl"
        done = {json.loads(l)["frame_id"] for l in cache.read_text(encoding="utf-8").splitlines() if l} if cache.is_file() else set()
        pending = [(i.frame_id, i.path) for i in items if i.frame_id not in done]
        if not pending:
            log.info("%s: all %d frames cached", name, len(items))
            continue
        teacher = resolve(name, kind_for(args.task, name))
        log.info("%s: %d frames to go (%d cached)", name, len(pending), len(done))
        started, n = time.time(), 0
        with cache.open("a", encoding="utf-8") as f:
            for p in teacher.predict(pending, args.prompt):
                f.write(json.dumps({"frame_id": p.frame_id, "text": p.text, "confidence": p.confidence,
                                    "error": p.error, "boxes": [[b.x, b.y, b.w, b.h, b.score] for b in p.boxes]},
                                   ensure_ascii=False) + "\n")
                n += 1
                if n % 50 == 0:
                    f.flush()
                    log.info("%s: %d/%d, %.2f s/frame", name, n, len(pending), (time.time() - started) / n)
        log.info("%s: done, %.2f s/frame", name, (time.time() - started) / max(n, 1))


if __name__ == "__main__":
    main()
