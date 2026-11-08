"""
Порт консенсуса из Labeling.Domain/Consensus (BoxConsensus.cs, TextConsensus.cs) на Python.

Платформа считает консенсус в C#; здесь та же логика нужна офлайн-экспериментам, чтобы цифры в отчёте
совпадали с тем, что сделала бы система. Поведение сверено с C#-тестами (tests/test_consensus.py
повторяет tests/Unit/Labeling/Domain/*ConsensusTests.cs). Меняешь одно — меняй и другое.
"""
from __future__ import annotations

from dataclasses import dataclass, field

from .common import Box, normalize_text

DEFAULT_IOU = 0.5
QUORUM = 0.5


@dataclass
class Vote:
    teacher: str
    weight: float
    boxes: list[Box] = field(default_factory=list)
    text: str | None = None


@dataclass
class Consensus:
    boxes: list[Box]
    text: str | None
    agreement: float


def _check(votes: list[Vote]) -> float:
    if not votes:
        raise ValueError("At least one teacher vote is required")
    if any(v.weight <= 0 for v in votes):
        raise ValueError("Teacher weights must be positive")
    return sum(v.weight for v in votes)


def box_consensus(votes: list[Vote], iou_threshold: float = DEFAULT_IOU) -> Consensus:
    total = _check(votes)
    # Стабильная сортировка, как OrderByDescending в LINQ.
    candidates = sorted(((v, b) for v in votes for b in v.boxes), key=lambda c: c[1].score * c[0].weight, reverse=True)

    clusters: list[list[tuple[Vote, Box]]] = []
    for vote, box in candidates:
        best, best_iou = None, -1.0
        for cluster in clusters:
            if any(m[0].teacher == vote.teacher for m in cluster):
                continue
            iou = cluster[0][1].iou(box)
            if iou >= iou_threshold and iou > best_iou:
                best, best_iou = cluster, iou
        if best is not None:
            best.append((vote, box))
        else:
            clusters.append([(vote, box)])

    if not clusters:
        return Consensus([], None, 1.0)

    accepted, decisiveness = [], 0.0
    for cluster in clusters:
        support = sum(v.weight for v, _ in cluster) / total
        decisiveness += max(support, 1 - support)
        if support > QUORUM:
            accepted.append(_fuse(cluster, support))
    return Consensus(accepted, None, round(decisiveness / len(clusters), 4))


def _fuse(cluster: list[tuple[Vote, Box]], support: float) -> Box:
    weights = [max(b.score, 1e-6) * v.weight for v, b in cluster]
    s = sum(weights)
    avg = lambda f: sum(f(b) * w for (_, b), w in zip(cluster, weights)) / s
    x, y = avg(lambda b: b.x), avg(lambda b: b.y)
    return Box(x, y, min(avg(lambda b: b.w), 1 - x), min(avg(lambda b: b.h), 1 - y), min(1.0, support))


def text_consensus(votes: list[Vote]) -> Consensus:
    total = _check(votes)
    groups: dict[str, float] = {}
    for v in votes:
        key = normalize_text(v.text)
        groups[key] = groups.get(key, 0.0) + v.weight
    # Порядок как в C#: вес ↓, непустой раньше пустого, затем ординально.
    text, weight = sorted(groups.items(), key=lambda kv: (-kv[1], kv[0] == "", kv[0]))[0]
    return Consensus([], text or None, round(weight / total, 4))


def fleiss_kappa(items: list[list[str]]) -> float:
    """Каппа Фляйса: согласие учителей с поправкой на случайное. Кадры с другим числом голосов пропускаются."""
    if not items:
        return 0.0
    n_raters = len(items[0])
    rows = [r for r in items if len(r) == n_raters]
    if n_raters < 2 or not rows:
        return 0.0
    cats = sorted({c for r in rows for c in r})
    if len(cats) < 2:
        return 1.0
    p_bar = sum((sum(r.count(c) ** 2 for c in cats) - n_raters) / (n_raters * (n_raters - 1)) for r in rows) / len(rows)
    p_e = sum((sum(r.count(c) for r in rows) / (len(rows) * n_raters)) ** 2 for c in cats)
    return 1.0 if abs(1 - p_e) < 1e-12 else (p_bar - p_e) / (1 - p_e)
