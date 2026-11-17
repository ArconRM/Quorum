"""
Главный эксперимент: ученик (YOLOv8n) обучается на метках ансамбля при разных стратегиях валидации,
качество меряется на кадрах, которых он не видел. Отвечает на вопрос работы — какую долю ручного труда
можно убрать без заметной потери качества ученика.

  python -m experiments.student --out runs/camera_v1 \\
      --max-area mm-grounding-dino-large=0.03,llmdet-large=0.03,owlv2-large=0.03 \\
      --cycle "2025-11-09,2025-11-15>2025-11-16" \\
      --cycle "2025-11-09,2025-11-15,2025-11-16>2025-11-29,2025-11-30" \\
      --strategies none,auto@0.85,hybrid@0.7,hybrid@0.85,manual

Вход — каталог прогона experiments.run (manifest.jsonl + predictions/). Кадры делятся на группы:
по умолчанию группа — домен кадра (для набора с камеры это день съёмки), --group-regex достаёт группу
из имени файла. Цикл «A,B>C»: учимся на метках групп A и B, проверяем на группе C по эталону.

Стратегии (человека изображает эталон, как в experiments.evaluate):
  none      все метки консенсуса принимаются без проверки;
  auto@T    кадры с согласием ≥ T принимаются, остальные отбрасываются;
  hybrid@T  кадры с согласием ≥ T принимаются, остальные размечает человек;
  manual    все кадры размечает человек (верхняя граница).

Пороги уверенности учителей нельзя подбирать по проверочным кадрам. Они либо задаются через --min-score,
либо подбираются на небольшой калибровочной выборке из обучающих групп первого цикла (--tune-frames):
это тоже ручная разметка, её объём печатается в сводке.

Результаты — <out>/<report>/: results.jsonl (кэш: прерванный запуск продолжается с места остановки),
results.csv, summary.md, cycles.png, tradeoff.png. Веса и наборы YOLO лежат в STUDENT_WORK_DIR
(по умолчанию /cache/quorum-student), в каталог прогона они не попадают.
"""
from __future__ import annotations

import argparse
import csv
import json
import logging
import os
import random
import re
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from quorum_worker.students import Sample, validation_split

from . import evaluate as ev
from .run import load_manifest, resolve_out

log = logging.getLogger("experiments.student")


# ---------------- стратегии и циклы ----------------

@dataclass(frozen=True)
class Strategy:
    name: str        # как в командной строке: none, auto@0.85, hybrid@0.7, manual
    kind: str        # none | auto | hybrid | manual
    threshold: float = 0.0


def parse_strategies(text: str) -> list[Strategy]:
    out = []
    for part in (p.strip() for p in text.split(",") if p.strip()):
        kind, _, value = part.partition("@")
        if kind in ("none", "manual") and not value:
            out.append(Strategy(part, kind))
        elif kind in ("auto", "hybrid") and value:
            out.append(Strategy(part, kind, float(value)))
        else:
            raise SystemExit(f"--strategies: не понял {part!r}; примеры: none, auto@0.85, hybrid@0.7, manual")
    return out


@dataclass(frozen=True)
class Cycle:
    index: int
    train: tuple[str, ...]
    test: tuple[str, ...]


def parse_cycles(specs: list[str]) -> list[Cycle]:
    cycles = []
    for i, spec in enumerate(specs, 1):
        left, sep, right = spec.partition(">")
        train = tuple(g.strip() for g in left.split(",") if g.strip())
        test = tuple(g.strip() for g in right.split(",") if g.strip())
        if not sep or not train or not test:
            raise SystemExit(f"--cycle: не понял {spec!r}; формат «обучающие,группы>проверочные,группы»")
        if set(train) & set(test):
            raise SystemExit(f"--cycle {spec!r}: группа одновременно в обучении и в проверке")
        cycles.append(Cycle(i, train, test))
    return cycles


def group_of(frame_id: str, domain: str, pattern: re.Pattern | None) -> str:
    """Группа кадра: домен или первая скобка регулярного выражения по имени файла (например, дата)."""
    if pattern is None:
        return domain
    m = pattern.search(frame_id)
    return m.group(1) if m else ""


def stem_of(frame_id: str) -> str:
    """frame_id в экспериментах — относительный путь; для имени файла в наборе YOLO убираем каталоги."""
    return str(Path(frame_id).with_suffix("")).replace("/", "__").replace("\\", "__")


# ---------------- сборка обучающей выборки ----------------

@dataclass
class Assembled:
    samples: list[Sample]
    pool: int            # кадров в обучающих группах цикла
    human: int           # сколько из них размечено человеком
    dropped: int         # отброшено (Auto)
    label_accuracy: float  # доля кадров выборки с полностью верной меткой


def assemble(frames: list[dict], strategy: Strategy) -> Assembled:
    """frames — элементы evaluate()['frames']: item (эталон), consensus, correct."""
    samples, human, dropped, right = [], 0, 0, 0
    for f in frames:
        item, consensus = f["item"], f["consensus"]
        confident = consensus.agreement >= strategy.threshold
        if strategy.kind == "manual" or (strategy.kind == "hybrid" and not confident):
            boxes, ok = item.boxes, True
            human += 1
        elif strategy.kind == "auto" and not confident:
            dropped += 1
            continue
        else:
            boxes, ok = consensus.boxes, f["correct"]
        right += ok
        samples.append(Sample(stem_of(item.frame_id), item.path, list(boxes)))
    return Assembled(samples, len(frames), human, dropped, round(right / max(len(samples), 1), 4))


# ---------------- обучение и оценка (Ultralytics) ----------------

def work_root(run_name: str) -> Path:
    base = os.getenv("STUDENT_WORK_DIR") or ("/cache/quorum-student" if Path("/cache").is_dir()
                                              else str(Path(tempfile.gettempdir()) / "quorum-student"))
    return Path(base) / run_name


def train_yolo(job_dir: Path, samples: list[Sample], start: str, epochs: int, imgsz: int, seed: int) -> str:
    """Обучает ученика и возвращает путь к лучшим весам. Лучшая эпоха выбирается по части тех же меток."""
    from ultralytics import YOLO
    from quorum_worker import vram
    from quorum_worker.students import yolo

    vram.slot.release()
    train_set, val_set = validation_split(samples)
    data = yolo.write_dataset(job_dir / "dataset", {"train": train_set, "val": val_set})
    model = YOLO(start)
    model.train(data=str(data), epochs=epochs, imgsz=imgsz, batch=min(yolo.BATCH, max(2, len(train_set))),
                device=yolo._device(), workers=yolo.WORKERS, project=str(job_dir), name="run", exist_ok=True,
                plots=False, verbose=False, amp=vram.device() == "cuda", seed=seed)
    trainer = model.trainer
    return str(trainer.best if Path(trainer.best).is_file() else trainer.last)


def eval_yolo(job_dir: Path, weights: str, samples: list[Sample], imgsz: int) -> dict:
    from ultralytics import YOLO
    from quorum_worker.students import yolo

    data = yolo.write_dataset(job_dir / "test", {"val": samples})
    m = YOLO(weights).val(data=str(data), split="val", imgsz=imgsz, batch=yolo.BATCH, device=yolo._device(),
                          workers=0, plots=False, verbose=False, project=str(job_dir), name="metrics", exist_ok=True)
    return {"map50": round(float(m.box.map50), 4), "map50_95": round(float(m.box.map), 4),
            "precision": round(float(m.box.mp), 4), "recall": round(float(m.box.mr), 4)}


# ---------------- эксперимент ----------------

def run_experiment(frames: list[dict], groups: dict[str, str], cycles: list[Cycle], strategies: list[Strategy],
                   results_file: Path, work: Path, epochs: int, imgsz: int, seed: int, init: str, base: str,
                   train_fn: Callable = train_yolo, eval_fn: Callable = eval_yolo) -> list[dict]:
    """Все пары (цикл, стратегия). Готовые строки берутся из results.jsonl — запуск можно прерывать."""
    done = {}
    if results_file.is_file():
        for line in results_file.read_text(encoding="utf-8").splitlines():
            if line:
                row = json.loads(line)
                done[(row["cycle"], row["strategy"])] = row
    by_group: dict[str, list[dict]] = {}
    for f in frames:
        by_group.setdefault(groups[f["item"].frame_id], []).append(f)
    for c in cycles:
        for g in c.train + c.test:
            if g not in by_group:
                raise SystemExit(f"Цикл {c.index}: нет кадров группы {g!r}; есть {sorted(by_group)}")

    rows = []
    for c in cycles:
        pool = [f for g in c.train for f in by_group[g]]
        test = [Sample(stem_of(f["item"].frame_id), f["item"].path, list(f["item"].boxes))
                for g in c.test for f in by_group[g]]
        for s in strategies:
            if (c.index, s.name) in done:
                rows.append(done[(c.index, s.name)])
                continue
            a = assemble(pool, s)
            row = {"cycle": c.index, "strategy": s.name, "train_groups": "+".join(c.train),
                   "test_groups": "+".join(c.test), "pool_frames": a.pool, "train_frames": len(a.samples),
                   "human_frames": a.human, "human_fraction": round(a.human / max(a.pool, 1), 4),
                   "dropped_frames": a.dropped, "label_accuracy": a.label_accuracy, "test_frames": len(test)}
            job = work / f"c{c.index}_{re.sub(r'[^0-9a-zA-Z]+', '_', s.name)}"
            if not any(x.boxes for x in a.samples):
                # Ни одной принятой метки с объектом — учить нечему, ученик остаётся необученным.
                row |= {"map50": 0.0, "map50_95": 0.0, "precision": 0.0, "recall": 0.0, "note": "нет меток с объектами"}
            else:
                previous = work / f"c{c.index - 1}_{re.sub(r'[^0-9a-zA-Z]+', '_', s.name)}" / "best.pt"
                start = str(previous) if init == "previous" and previous.is_file() else base
                log.info("Cycle %d, %s: %d frames (%d human, %d dropped), from %s", c.index, s.name,
                         len(a.samples), a.human, a.dropped, start)
                weights = train_fn(job, a.samples, start, epochs, imgsz, seed)
                job.mkdir(parents=True, exist_ok=True)
                if Path(weights).is_file() and Path(weights) != job / "best.pt":
                    (job / "best.pt").write_bytes(Path(weights).read_bytes())
                row |= eval_fn(job, weights, test, imgsz)
            log.info("Cycle %d, %s: mAP50 %.4f", c.index, s.name, row["map50"])
            with results_file.open("a", encoding="utf-8") as f:
                f.write(json.dumps(row, ensure_ascii=False) + "\n")
            rows.append(row)
    return rows


# ---------------- отчёт ----------------

def write_report(rows: list[dict], report: Path, header: list[str]) -> None:
    keys = list(dict.fromkeys(k for r in rows for k in r))
    with (report / "results.csv").open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=keys)
        w.writeheader()
        w.writerows(rows)

    cycles = sorted({r["cycle"] for r in rows})
    strategies = list(dict.fromkeys(r["strategy"] for r in rows))
    cell = {(r["cycle"], r["strategy"]): r for r in rows}
    text = ["# Ученик на метках ансамбля", "", *header, "", "## mAP50 ученика на проверочных группах", "",
            "| стратегия | " + " | ".join(f"цикл {c}" for c in cycles) + " | среднее |",
            "|---|" + "---|" * (len(cycles) + 1)]
    for s in strategies:
        values = [cell[(c, s)]["map50"] for c in cycles if (c, s) in cell]
        text.append(f"| {s} | " + " | ".join(f"{cell[(c, s)]['map50']:.3f}" if (c, s) in cell else "" for c in cycles)
                    + f" | {sum(values) / max(len(values), 1):.3f} |")
    text += ["", "## Ручной труд и чистота меток (нарастающим итогом по обучающим группам цикла)", "",
             "| цикл | стратегия | кадров в обучении | человеку | доля человеку | отброшено | меток верно | mAP50 | mAP50-95 | P | R |",
             "|---|---|---|---|---|---|---|---|---|---|---|"]
    for r in rows:
        text.append(f"| {r['cycle']} | {r['strategy']} | {r['train_frames']} | {r['human_frames']} | "
                    f"{r['human_fraction']:.1%} | {r['dropped_frames']} | {r['label_accuracy']:.1%} | "
                    f"{r['map50']:.3f} | {r['map50_95']:.3f} | {r['precision']:.3f} | {r['recall']:.3f} |")
    text += ["", "Циклы:", ""] + [f"- цикл {c}: обучение на {cell[(c, strategies[0])]['train_groups']}, "
                                   f"проверка на {cell[(c, strategies[0])]['test_groups']} "
                                   f"({cell[(c, strategies[0])]['test_frames']} кадров)"
                                   for c in cycles if (c, strategies[0]) in cell]
    (report / "summary.md").write_text("\n".join(text) + "\n", encoding="utf-8")

    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except ImportError:
        print("! matplotlib не установлен — графики пропущены")
        return
    fig, ax = plt.subplots(figsize=(8, 4.5))
    for s in strategies:
        pts = [(c, cell[(c, s)]["map50"]) for c in cycles if (c, s) in cell]
        ax.plot([p[0] for p in pts], [p[1] for p in pts], "o-", label=s)
    ax.set_xticks(cycles)
    ax.set_xlabel("Цикл адаптации")
    ax.set_ylabel("mAP50 ученика на новых кадрах")
    ax.set_title("Качество ученика по циклам")
    ax.grid(alpha=0.3)
    ax.legend(fontsize=8)
    fig.tight_layout()
    fig.savefig(report / "cycles.png", dpi=150)
    plt.close(fig)

    fig, ax = plt.subplots(figsize=(7, 4.5))
    for s in strategies:
        mine = [cell[(c, s)] for c in cycles if (c, s) in cell]
        x = sum(r["human_fraction"] for r in mine) / len(mine)
        y = sum(r["map50"] for r in mine) / len(mine)
        ax.plot([x], [y], "o", markersize=9)
        ax.annotate(s, (x, y), textcoords="offset points", xytext=(6, 5), fontsize=8)
    ax.set_xlabel("Доля кадров, размеченных человеком (среднее по циклам)")
    ax.set_ylabel("mAP50 ученика (среднее по циклам)")
    ax.set_xlim(-0.03, 1.05)
    ax.set_title("Ручной труд ↔ качество ученика")
    ax.grid(alpha=0.3)
    fig.tight_layout()
    fig.savefig(report / "tradeoff.png", dpi=150)
    plt.close(fig)


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", type=Path, required=True, help="каталог прогона experiments.run")
    ap.add_argument("--cycle", action="append", default=[], help="«обучающие,группы>проверочные,группы»; можно несколько раз")
    ap.add_argument("--strategies", default="none,auto@0.85,hybrid@0.7,hybrid@0.85,manual")
    ap.add_argument("--group-regex", default="", help="регулярное выражение с одной скобкой: группа из имени файла вместо домена")
    ap.add_argument("--teachers", default="", help="только эти учителя, через запятую")
    ap.add_argument("--weights", default="", help="имя=вес через запятую")
    ap.add_argument("--min-score", default="", help="имя=порог через запятую; учителя без порога подбираются (--tune-frames)")
    ap.add_argument("--max-area", default="", help="имя=доля кадра через запятую")
    ap.add_argument("--tune-frames", type=int, default=200,
                    help="размер калибровочной выборки из обучающих групп первого цикла для подбора порогов; 0 — не подбирать")
    ap.add_argument("--epochs", type=int, default=30)
    ap.add_argument("--imgsz", type=int, default=int(os.getenv("YOLO_IMGSZ", "640")))
    ap.add_argument("--base", default="yolov8n.pt", help="веса необученного ученика")
    ap.add_argument("--init", choices=["fresh", "previous"], default="fresh",
                    help="fresh — каждый цикл учим с --base на всех накопленных метках; previous — дообучаем веса прошлого цикла")
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--report", default="student", help="имя каталога отчёта внутри --out")
    ap.add_argument("--groups", action="store_true", help="только показать группы и число кадров в них")
    args = ap.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    parse = lambda text: {k.strip(): float(v) for k, v in (p.split("=") for p in text.split(",") if "=" in p)}
    out = resolve_out(args.out)

    pattern = re.compile(args.group_regex) if args.group_regex else None
    items = load_manifest(out)
    groups = {i.frame_id: group_of(i.frame_id, i.domain, pattern) for i in items}
    if args.groups or not args.cycle:
        counts: dict[str, list[int]] = {}
        for i in items:
            c = counts.setdefault(groups[i.frame_id], [0, 0])
            c[0] += 1
            c[1] += bool(i.boxes)
        print("Группы (кадров всего / с объектами):")
        for g in sorted(counts):
            print(f"  {g or '<без группы>'}: {counts[g][0]} / {counts[g][1]}")
        if not args.cycle:
            print("Задай циклы: --cycle «обучающие,группы>проверочные,группы»")
        return

    cycles = parse_cycles(args.cycle)
    strategies = parse_strategies(args.strategies)
    only = [t.strip() for t in args.teachers.split(",") if t.strip()]
    weights, min_scores, max_area = parse(args.weights), parse(args.min_score), parse(args.max_area)

    header = []
    if args.tune_frames > 0:
        candidates = sorted(i.frame_id for i in items if groups[i.frame_id] in cycles[0].train)
        random.Random(args.seed).shuffle(candidates)
        calibration = set(candidates[:args.tune_frames])
        min_scores, accuracy = ev.tune_min_scores(out, weights, min_scores, only, max_area, calibration)
        header.append(f"Калибровка порогов учителей: {len(calibration)} кадров из групп {'+'.join(cycles[0].train)}, "
                      f"размеченных человеком (кадров верно на калибровке: {accuracy:.4f}).")
    header.append(f"Пороги уверенности учителей: {min_scores or 'как при прогоне'}; "
                  f"максимальная площадь рамки: {max_area or 'без ограничения'}; веса: {weights or 'равные'}.")
    header.append(f"Ученик: {args.base}, эпох {args.epochs}, размер входа {args.imgsz}, "
                  f"{'каждый цикл с нуля на накопленных метках' if args.init == 'fresh' else 'дообучение весов прошлого цикла'}.")
    for line in header:
        log.info(line)

    result = ev.evaluate(out, weights, min_scores, only, max_area)
    report = out / args.report
    report.mkdir(exist_ok=True)
    (report / "config.json").write_text(json.dumps(vars(args) | {"out": str(out), "min_score_used": min_scores},
                                                   ensure_ascii=False, indent=2, default=str), encoding="utf-8")
    rows = run_experiment(result["frames"], groups, cycles, strategies, report / "results.jsonl",
                          work_root(out.name + "_" + args.report), args.epochs, args.imgsz, args.seed, args.init, args.base)
    write_report(rows, report, header)
    print((report / "summary.md").read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
