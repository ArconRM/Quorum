"""
Метрики по кэшу предсказаний (без GPU, секунды). Консенсус — тот же, что в платформе (quorum_worker.consensus).

  python -m experiments.evaluate --out runs/detect_de [--weights grounding-dino-base=1,owlv2-base=0.8]

Что считается и куда пишется (всё в <out>/report/):
  teachers.csv    качество каждого учителя и консенсуса против эталона
  strategies.csv  перебор порога согласия для Auto и Hybrid: доля кадров человеку, точность меток,
                  сколько кадров ансамбль принял уверенно, но ошибочно (коррелированные ошибки)
  domains.csv     точность учителей и консенсуса по доменам (странам, дням съёмки)
  agreement.png   распределение согласия для верных и неверных консенсусных меток
  tradeoff.png    главный график: доля ручного труда ↔ точность итоговых меток
  summary.md      сводка для отчёта

Кадр размечен верно:
  детекция — каждый эталонный бокс найден (IoU ≥ 0.5) и лишних нет;
  текст    — консенсусная строка совпадает с эталоном после нормализации.
"""
from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path

from quorum_worker.common import Box, normalize_text
from quorum_worker.consensus import Vote, box_consensus, fleiss_kappa, text_consensus

from .datasets import Item
from .run import load_manifest

IOU = 0.5
THRESHOLDS = [round(0.05 * i, 2) for i in range(0, 21)]


# ---------------- сопоставление и метрики ----------------

def match(pred: list[Box], truth: list[Box], iou: float = IOU) -> tuple[int, int, int]:
    """Жадно по убыванию score: TP, FP, FN."""
    used, tp = set(), 0
    for p in sorted(pred, key=lambda b: -b.score):
        best, best_iou = None, iou
        for j, t in enumerate(truth):
            if j not in used and (v := p.iou(t)) >= best_iou:
                best, best_iou = j, v
        if best is not None:
            used.add(best)
            tp += 1
    return tp, len(pred) - tp, len(truth) - tp


def average_precision(scored: list[tuple[float, bool]], n_truth: int) -> float:
    """AP@0.5 с интерполяцией по всем точкам (как в COCO/VOC 2010+)."""
    if n_truth == 0:
        return 0.0
    scored = sorted(scored, key=lambda s: -s[0])
    tp = fp = 0
    points = []
    for _, ok in scored:
        tp, fp = tp + ok, fp + (not ok)
        points.append((tp / n_truth, tp / (tp + fp)))
    ap, prev_r = 0.0, 0.0
    for i, (r, _) in enumerate(points):
        ap += (r - prev_r) * max(p for _, p in points[i:])
        prev_r = r
    return ap


def scored_matches(pred: list[Box], truth: list[Box]) -> list[tuple[float, bool]]:
    used, out = set(), []
    for p in sorted(pred, key=lambda b: -b.score):
        best, best_iou = None, IOU
        for j, t in enumerate(truth):
            if j not in used and (v := p.iou(t)) >= best_iou:
                best, best_iou = j, v
        if best is not None:
            used.add(best)
        out.append((p.score, best is not None))
    return out


def cer(truth: str, pred: str) -> float:
    if not truth:
        return 0.0 if not pred else 1.0
    prev = list(range(len(pred) + 1))
    for i, a in enumerate(truth, 1):
        cur = [i]
        for j, b in enumerate(pred, 1):
            cur.append(min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (a != b)))
        prev = cur
    return prev[-1] / len(truth)


# ---------------- загрузка кэша ----------------

def load_predictions(out: Path) -> dict[str, dict[str, dict]]:
    preds = {}
    for f in sorted((out / "predictions").glob("*.jsonl")):
        rows = [json.loads(l) for l in f.read_text(encoding="utf-8").splitlines() if l]
        preds[f.stem] = {r["frame_id"]: r for r in rows}
    return preds


def boxes_of(row: dict | None) -> list[Box]:
    return [Box(*b) for b in row["boxes"]] if row else []


# ---------------- основной расчёт ----------------

def evaluate(out: Path, weights: dict[str, float]) -> dict:
    items = load_manifest(out)
    preds = load_predictions(out)
    if not preds:
        raise SystemExit(f"No predictions in {out / 'predictions'}: run experiments.run first")
    task = "detection" if any(i.boxes for i in items) or not any(i.text for i in items) else "ocr"
    teachers = sorted(preds)
    missing = {t: sum(i.frame_id not in preds[t] for i in items) for t in teachers}
    for t, m in missing.items():
        if m:
            print(f"! {t}: нет предсказаний для {m} кадров — считаются голосом «ничего нет»")

    frames = []  # по кадру: эталон, ответы учителей, консенсус, верен ли
    for it in items:
        votes = [Vote(t, weights.get(t, 1.0), boxes_of(preds[t].get(it.frame_id)), (preds[t].get(it.frame_id) or {}).get("text"))
                 for t in teachers]
        if task == "detection":
            c = box_consensus(votes)
            correct = match(c.boxes, it.boxes)[1:] == (0, 0)
        else:
            c = text_consensus(votes)
            correct = (c.text or "") == it.text
        frames.append({"item": it, "votes": votes, "consensus": c, "correct": correct})

    teacher_rows = [_teacher_metrics(task, t, items, preds[t]) for t in teachers]
    teacher_rows.append(_consensus_metrics(task, frames))
    sweep = [_strategy_row(frames, th) for th in THRESHOLDS]
    domains = _domain_rows(task, teachers, items, preds, frames)
    kappa = fleiss_kappa([[normalize_text(v.text) or "<none>" for v in f["votes"]] for f in frames]) if task == "ocr" else None
    return {"task": task, "teachers": teacher_rows, "sweep": sweep, "domains": domains, "frames": frames,
            "kappa": kappa, "n": len(items), "weights": {t: weights.get(t, 1.0) for t in teachers}}


def _teacher_metrics(task: str, name: str, items: list[Item], rows: dict) -> dict:
    if task == "detection":
        tp = fp = fn = exact = 0
        scored, n_truth = [], 0
        for it in items:
            pred = boxes_of(rows.get(it.frame_id))
            a, b, c = match(pred, it.boxes)
            tp, fp, fn, exact = tp + a, fp + b, fn + c, exact + (b == c == 0)
            scored += scored_matches(pred, it.boxes)
            n_truth += len(it.boxes)
        p, r = tp / max(tp + fp, 1), tp / max(tp + fn, 1)
        return {"teacher": name, "precision": round(p, 4), "recall": round(r, 4),
                "f1": round(2 * p * r / max(p + r, 1e-9), 4), "ap50": round(average_precision(scored, n_truth), 4),
                "frame_accuracy": round(exact / len(items), 4)}
    texts = [normalize_text((rows.get(it.frame_id) or {}).get("text")) for it in items]
    return {"teacher": name,
            "exact_match": round(sum(t == it.text for t, it in zip(texts, items)) / len(items), 4),
            "cer": round(sum(cer(it.text, t) for t, it in zip(texts, items)) / len(items), 4),
            "read_rate": round(sum(bool(t) for t in texts) / len(items), 4),
            "frame_accuracy": round(sum(t == it.text for t, it in zip(texts, items)) / len(items), 4)}


def _consensus_metrics(task: str, frames: list[dict]) -> dict:
    n = len(frames)
    if task == "detection":
        tp = fp = fn = 0
        for f in frames:
            a, b, c = match(f["consensus"].boxes, f["item"].boxes)
            tp, fp, fn = tp + a, fp + b, fn + c
        p, r = tp / max(tp + fp, 1), tp / max(tp + fn, 1)
        return {"teacher": "CONSENSUS", "precision": round(p, 4), "recall": round(r, 4),
                "f1": round(2 * p * r / max(p + r, 1e-9), 4), "ap50": "",
                "frame_accuracy": round(sum(f["correct"] for f in frames) / n, 4)}
    texts = [f["consensus"].text or "" for f in frames]
    return {"teacher": "CONSENSUS",
            "exact_match": round(sum(f["correct"] for f in frames) / n, 4),
            "cer": round(sum(cer(f["item"].text, t) for f, t in zip(frames, texts)) / n, 4),
            "read_rate": round(sum(bool(t) for t in texts) / n, 4),
            "frame_accuracy": round(sum(f["correct"] for f in frames) / n, 4)}


def _strategy_row(frames: list[dict], threshold: float) -> dict:
    """
    Один порог — две стратегии платформы (ValidationStrategy.Decide):
      Auto:   согласие ≥ порога → принять, иначе отбросить;
      Hybrid: согласие ≥ порога → принять, иначе человеку (человек отвечает эталоном).
    """
    n = len(frames)
    accepted = [f for f in frames if f["consensus"].agreement >= threshold]
    acc_ok = sum(f["correct"] for f in accepted)
    human = n - len(accepted)
    return {
        "threshold": threshold,
        "auto_coverage": round(len(accepted) / n, 4),                     # доля кадров, попавших в обучение
        "auto_precision": round(acc_ok / len(accepted), 4) if accepted else "",  # чистота принятых меток
        "auto_confident_wrong": len(accepted) - acc_ok,                   # уверенно и ошибочно
        "hybrid_human_fraction": round(human / n, 4),
        "hybrid_label_accuracy": round((acc_ok + human) / n, 4),
    }


def _domain_rows(task, teachers, items, preds, frames) -> list[dict]:
    domains = sorted({i.domain for i in items})
    rows = []
    for d in domains:
        idx = [k for k, it in enumerate(items) if it.domain == d]
        row = {"domain": d, "frames": len(idx)}
        for t in teachers:
            if task == "detection":
                ok = sum(match(boxes_of(preds[t].get(items[k].frame_id)), items[k].boxes)[1:] == (0, 0) for k in idx)
            else:
                ok = sum(normalize_text((preds[t].get(items[k].frame_id) or {}).get("text")) == items[k].text for k in idx)
            row[t] = round(ok / len(idx), 4)
        row["CONSENSUS"] = round(sum(frames[k]["correct"] for k in idx) / len(idx), 4)
        row["mean_agreement"] = round(sum(frames[k]["consensus"].agreement for k in idx) / len(idx), 4)
        rows.append(row)
    return rows


# ---------------- вывод ----------------

def write_csv(path: Path, rows: list[dict]) -> None:
    if not rows:
        return
    keys = list(dict.fromkeys(k for r in rows for k in r))
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=keys)
        w.writeheader()
        w.writerows(rows)


def plots(result: dict, report: Path) -> None:
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except ImportError:
        print("! matplotlib не установлен — графики пропущены")
        return

    frames = result["frames"]
    ok = [f["consensus"].agreement for f in frames if f["correct"]]
    bad = [f["consensus"].agreement for f in frames if not f["correct"]]
    fig, ax = plt.subplots(figsize=(7, 4))
    bins = [i / 20 for i in range(21)]
    ax.hist([ok, bad], bins=bins, stacked=True, label=["консенсус верен", "консенсус ошибся"], color=["#4c78a8", "#e45756"])
    ax.set_xlabel("Согласие ансамбля")
    ax.set_ylabel("Кадров")
    ax.set_title("Согласие учителей для верных и ошибочных меток")
    ax.legend()
    fig.tight_layout()
    fig.savefig(report / "agreement.png", dpi=150)
    plt.close(fig)

    sweep = result["sweep"]
    fig, (left, right) = plt.subplots(1, 2, figsize=(12, 4.8))

    # Hybrid: несколько порогов часто дают одну точку — подписываем точку диапазоном порогов.
    hy = _group(sweep, "hybrid_human_fraction", "hybrid_label_accuracy")
    left.plot([x for x, _, _ in hy], [y for _, y, _ in hy], "o-", color="#4c78a8", label="Hybrid: спорные — человеку")
    left.plot([1], [1], "D", color="#54a24b", markersize=9, label="Manual: всё — человеку")
    _annotate(left, hy)
    left.set_xlabel("Доля кадров, проверенных человеком")
    left.set_ylabel("Точность меток обучающей выборки")
    left.set_xlim(-0.03, 1.05)
    left.set_title("Трудозатраты ↔ качество разметки")

    auto = _group([r for r in sweep if r["auto_precision"] != ""], "auto_coverage", "auto_precision")
    right.plot([x for x, _, _ in auto], [y for _, y, _ in auto], "s-", color="#e45756", label="Auto: спорные отбрасываются")
    _annotate(right, auto)
    right.set_xlabel("Доля кадров, попавших в обучение")
    right.set_ylabel("Чистота принятых меток")
    right.set_xlim(-0.03, 1.05)
    right.set_title("Без человека: объём ↔ чистота")

    for ax in (left, right):
        ax.grid(alpha=0.3)
        ax.legend(loc="best", fontsize=8)
    fig.tight_layout()
    fig.savefig(report / "tradeoff.png", dpi=150)
    plt.close(fig)


def _group(rows: list[dict], xk: str, yk: str) -> list[tuple[float, float, str]]:
    """Точки по возрастанию x; одинаковые точки склеиваются, подпись — диапазон порогов."""
    points: dict[tuple, list[float]] = {}
    for r in rows:
        points.setdefault((r[xk], r[yk]), []).append(r["threshold"])
    out = []
    for (x, y), ths in sorted(points.items()):
        label = f"{min(ths):.2f}" if len(ths) == 1 else f"{min(ths):.2f}–{max(ths):.2f}"
        out.append((x, y, label))
    return out


def _annotate(ax, points) -> None:
    for x, y, label in points:
        ax.annotate(label, (x, y), textcoords="offset points", xytext=(5, -11), fontsize=7, color="#555")


def summary(result: dict, report: Path) -> None:
    def table(rows):
        keys = list(dict.fromkeys(k for r in rows for k in r))
        lines = ["| " + " | ".join(keys) + " |", "|" + "---|" * len(keys)]
        lines += ["| " + " | ".join(str(r.get(k, "")) for k in keys) + " |" for r in rows]
        return "\n".join(lines)

    best = max((r for r in result["sweep"] if r["auto_precision"] != ""), key=lambda r: r["hybrid_label_accuracy"] - r["hybrid_human_fraction"])
    text = [f"# Разметка ансамблем: {result['task']}, {result['n']} кадров", "",
            f"Веса учителей: {result['weights']}", "",
            "## Учителя и консенсус", "", table(result["teachers"]), ""]
    if result["kappa"] is not None:
        text += [f"Каппа Фляйса по ответам учителей: **{result['kappa']:.3f}**", ""]
    text += ["## Стратегии валидации по порогу согласия", "",
             "Человек симулируется эталоном. Manual: 100% кадров человеку, точность 1.0.", "",
             table(result["sweep"]), "",
             f"Лучший баланс Hybrid по (точность − доля человека): порог {best['threshold']}, "
             f"человеку {best['hybrid_human_fraction']:.0%} кадров, точность меток {best['hybrid_label_accuracy']:.1%}.", "",
             "## По доменам", "", table(result["domains"]), ""]
    (report / "summary.md").write_text("\n".join(text), encoding="utf-8")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--weights", default="", help="имя=вес через запятую; по умолчанию все 1")
    args = ap.parse_args()
    weights = {k.strip(): float(v) for k, v in (p.split("=") for p in args.weights.split(",") if "=" in p)}

    result = evaluate(args.out, weights)
    report = args.out / "report"
    report.mkdir(exist_ok=True)
    write_csv(report / "teachers.csv", result["teachers"])
    write_csv(report / "strategies.csv", result["sweep"])
    write_csv(report / "domains.csv", result["domains"])
    plots(result, report)
    summary(result, report)
    print((report / "summary.md").read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
