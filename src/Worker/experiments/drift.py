"""
Зачем адаптация и почему периодическая: матрица «модель × день съёмки».

Каждая модель проверяется на каждом дне камеры по эталону:
  - модель до адаптации — например, YOLOv8n из курсовой, обученная на общих наборах номеров;
  - модели учеников по циклам из прогона experiments.student (веса лежат в STUDENT_WORK_DIR).
Дни, на которых модель обучалась, помечаются «*»: там оценка завышена. По строке модели первого цикла
видно, как она деградирует на поздних днях (смена условий, объектив), по диагонали — что даёт
переобучение на новых данных.

  python -m experiments.drift --out runs/camera_v1 --report student --strategies none,manual \\
      --model "курсовая YOLOv8n=/models/coursework-yolov8n.pt"

Результат — <out>/<report>/drift.md и drift.csv. Модели и дни можно ограничить флагами.
"""
from __future__ import annotations

import argparse
import csv
import json
import logging
import re
from pathlib import Path
from typing import Callable

from quorum_worker.students import Sample

from .run import load_manifest, resolve_out
from .student import parse_cycles, stem_of, work_root

log = logging.getLogger("experiments.drift")


def yolo_eval(data_yaml: Path, weights: str, imgsz: int, project: Path) -> dict:
    from ultralytics import YOLO
    from quorum_worker.students import yolo

    # single_cls: у модели из курсовой может быть свой список классов — считаем любой бокс номером.
    m = YOLO(weights).val(data=str(data_yaml), split="val", imgsz=imgsz, batch=yolo.BATCH, device=yolo._device(),
                          workers=0, plots=False, verbose=False, single_cls=True,
                          project=str(project), name="metrics", exist_ok=True)
    return {"map50": round(float(m.box.map50), 4), "map50_95": round(float(m.box.map), 4),
            "precision": round(float(m.box.mp), 4), "recall": round(float(m.box.mr), 4)}


def student_models(report_dir: Path, work: Path, strategies: list[str]) -> list[tuple[str, str, set[str]]]:
    """(имя, веса, дни обучения) для каждого цикла и стратегии прогона experiments.student."""
    config = json.loads((report_dir / "config.json").read_text(encoding="utf-8"))
    out = []
    for c in parse_cycles(config["cycle"]):
        for s in strategies:
            weights = work / f"c{c.index}_{re.sub(r'[^0-9a-zA-Z]+', '_', s)}" / "best.pt"
            if weights.is_file():
                out.append((f"цикл {c.index}, {s}", str(weights), set(c.train)))
            else:
                log.warning("Нет весов %s — пропускаю", weights)
    return out


def run(items, models: list[tuple[str, str, set[str]]], days: list[str], work: Path, imgsz: int,
        eval_fn: Callable = yolo_eval, results: Path | None = None) -> list[dict]:
    from quorum_worker.students import yolo

    done = {}
    if results is not None and results.is_file():
        for line in results.read_text(encoding="utf-8").splitlines():
            if line:
                r = json.loads(line)
                done[(r["model"], r["day"])] = r
    data = {}
    for day in days:
        samples = [Sample(stem_of(i.frame_id), i.path, list(i.boxes)) for i in items if i.domain == day]
        data[day] = yolo.write_dataset(work / "days" / day, {"val": samples})
    rows = []
    for name, weights, trained in models:
        for day in days:
            if (name, day) in done:
                rows.append(done[(name, day)])
                continue
            row = {"model": name, "day": day, "in_train": day in trained} | eval_fn(data[day], weights, imgsz, work / "eval")
            log.info("%s on %s: mAP50 %.4f", name, day, row["map50"])
            if results is not None:
                with results.open("a", encoding="utf-8") as f:
                    f.write(json.dumps(row, ensure_ascii=False) + "\n")
            rows.append(row)
    return rows


def write_report(rows: list[dict], report: Path, days: list[str]) -> None:
    with (report / "drift.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0]))
        w.writeheader()
        w.writerows(rows)
    cell = {(r["model"], r["day"]): r for r in rows}
    models = list(dict.fromkeys(r["model"] for r in rows))
    text = ["# Модели по дням съёмки", "", "«*» — день был в обучении модели (оценка завышена).", ""]
    for key, title in (("map50", "mAP50"), ("map50_95", "mAP50-95"), ("recall", "Полнота")):
        text += [f"## {title}", "", "| модель | " + " | ".join(d[5:] for d in days) + " |",
                 "|---|" + "---|" * len(days)]
        for m in models:
            text.append(f"| {m} | " + " | ".join(
                (f"{cell[(m, d)][key]:.3f}" + ("*" if cell[(m, d)]["in_train"] else "")) if (m, d) in cell else ""
                for d in days) + " |")
        text.append("")
    (report / "drift.md").write_text("\n".join(text), encoding="utf-8")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", type=Path, required=True, help="каталог прогона experiments.run")
    ap.add_argument("--report", default="student", help="отчёт experiments.student, чьих учеников проверять")
    ap.add_argument("--strategies", default="none,manual", help="стратегии учеников через запятую; пусто — без учеников")
    ap.add_argument("--model", action="append", default=[], help="«имя=путь к весам» — модель вне циклов, можно несколько")
    ap.add_argument("--days", default="", help="только эти дни через запятую")
    ap.add_argument("--imgsz", type=int, default=640)
    args = ap.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    out = resolve_out(args.out)
    items = load_manifest(out)
    days = sorted({i.domain for i in items})
    if args.days:
        days = [d for d in days if d in set(args.days.split(","))]
    report = out / args.report
    report.mkdir(exist_ok=True)

    models = []
    for spec in args.model:
        name, sep, path = spec.partition("=")
        if not sep or not Path(path).is_file():
            raise SystemExit(f"--model {spec!r}: нужен формат «имя=путь», и файл должен существовать")
        models.append((name.strip(), path, set()))
    strategies = [s.strip() for s in args.strategies.split(",") if s.strip()]
    if strategies:
        models += student_models(report, work_root(out.name + "_" + args.report), strategies)
    if not models:
        raise SystemExit("Нечего проверять: задай --model или --strategies")

    rows = run(items, models, days, work_root(out.name + "_" + args.report) / "drift", args.imgsz,
               results=report / "drift.jsonl")
    write_report(rows, report, days)
    print((report / "drift.md").read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
