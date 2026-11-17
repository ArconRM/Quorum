import json

import pytest

from experiments.datasets import load_coco
from experiments.evaluate import SCORE_GRID, evaluate, split_holdout, tune_min_scores
from experiments.run import load_manifest


def _roboflow(tmp_path):
    from PIL import Image

    d = tmp_path / "train"
    d.mkdir()
    for name in ("plate.jpg", "container.jpg", "both.jpg"):
        Image.new("RGB", (100, 50)).save(d / name)
    (d / "_annotations.coco.json").write_text(json.dumps({
        # Как в экспорте Roboflow: имя набора — «надкласс» с id 0 и то же имя у настоящего класса.
        "categories": [{"id": 0, "name": "Plate"}, {"id": 1, "name": "ContainerCode"}, {"id": 2, "name": "Plate"}],
        "images": [{"id": 1, "file_name": "plate.jpg", "width": 100, "height": 50},
                   {"id": 2, "file_name": "container.jpg", "width": 100, "height": 50},
                   {"id": 3, "file_name": "both.jpg", "width": 100, "height": 50}],
        "annotations": [{"id": 1, "image_id": 1, "category_id": 2, "bbox": [10, 5, 20, 10]},
                        {"id": 2, "image_id": 2, "category_id": 1, "bbox": [0, 0, 5, 40]},
                        {"id": 3, "image_id": 3, "category_id": 2, "bbox": [10, 5, 20, 10]},
                        {"id": 4, "image_id": 3, "category_id": 1, "bbox": [0, 0, 5, 40]}]}))
    return tmp_path


def test_coco_classes_keep_only_wanted_boxes_and_frames(tmp_path):
    root = _roboflow(tmp_path)
    assert len(load_coco(root)) == 3                      # без фильтра — как раньше, все классы
    items = {i.frame_id: i for i in load_coco(root, ["plate"])}
    assert sorted(items) == ["train/both.jpg", "train/plate.jpg"]
    assert len(items["train/both.jpg"].boxes) == 1 and items["train/both.jpg"].boxes[0].w == 0.2
    assert load_coco(root, ["no-such-class"]) == []


def _run_dir(tmp_path, n=40):
    """Учитель a ставит верную рамку с 0.9 и ложную с 0.4; учитель b — только верную."""
    truth, false = [0.1, 0.1, 0.2, 0.2], [0.6, 0.6, 0.2, 0.2]
    out = tmp_path / "run"
    (out / "predictions").mkdir(parents=True)
    ids = [f"f{k:02d}.jpg" for k in range(n)]
    (out / "manifest.jsonl").write_text("".join(json.dumps(
        {"frame_id": i, "path": i, "text": "", "domain": "d", "boxes": [truth]}) + "\n" for i in ids))
    row = lambda i, boxes: json.dumps({"frame_id": i, "text": None, "confidence": 0.9, "error": None, "boxes": boxes}) + "\n"
    (out / "predictions" / "a.jsonl").write_text("".join(row(i, [truth + [0.9], false + [0.4]]) for i in ids))
    (out / "predictions" / "b.jsonl").write_text("".join(row(i, [truth + [0.8]]) for i in ids))
    return out


def test_holdout_split_is_disjoint_and_reproducible(tmp_path):
    items = load_manifest(_run_dir(tmp_path))
    tune, test = split_holdout(items, 0.25, seed=1)
    assert len(test) == 10 and len(tune) == 30 and not tune & test
    assert (tune, test) == split_holdout(items, 0.25, seed=1)
    assert test != split_holdout(items, 0.25, seed=2)[1]


def test_tuning_respects_fixed_thresholds_and_reports_on_holdout_only(tmp_path):
    out = _run_dir(tmp_path)
    tune, test = split_holdout(load_manifest(out), 0.5)
    tuned, accuracy = tune_min_scores(out, {}, {"b": 0.3}, None, {}, tune)
    assert tuned["b"] == 0.3 and tuned["a"] in SCORE_GRID and accuracy == 1.0
    result = evaluate(out, {}, tuned, None, {}, keep=test)
    assert result["n"] == len(test) == 20
    assert result["teachers"][-1]["frame_accuracy"] == 1.0


def test_tuning_raises_threshold_when_it_removes_false_boxes(tmp_path):
    out = _run_dir(tmp_path)
    ids = {i.frame_id for i in load_manifest(out)}
    # С весом 3 учитель a один набирает кворум, и его ложная рамка 0.4 проходит в консенсус,
    # пока порог не поднят выше 0.4.
    tuned, accuracy = tune_min_scores(out, {"a": 3.0}, {}, None, {}, ids)
    assert tuned["a"] > 0.4 and accuracy == 1.0


# ---------------- experiments.student ----------------

def _student_frames(tmp_path):
    """Два дня по 10 кадров. В каждом дне у 4 кадров учитель b молчит: согласие 0.5, метка консенсуса пустая (неверная)."""
    from experiments.evaluate import evaluate

    truth = [0.1, 0.1, 0.2, 0.2]
    out = tmp_path / "run"
    (out / "predictions").mkdir(parents=True)
    ids = [f"d{d}/images/f{k:02d}.jpg" for d in (1, 2) for k in range(10)]
    (out / "manifest.jsonl").write_text("".join(json.dumps(
        {"frame_id": i, "path": "/x/" + i, "text": "", "domain": i.split("/")[0], "boxes": [truth]}) + "\n" for i in ids))
    row = lambda i, boxes: json.dumps({"frame_id": i, "text": None, "confidence": 0.9, "error": None, "boxes": boxes}) + "\n"
    (out / "predictions" / "a.jsonl").write_text("".join(row(i, [truth + [0.9]]) for i in ids))
    (out / "predictions" / "b.jsonl").write_text("".join(
        row(i, [] if int(i[-6:-4]) < 4 else [truth + [0.8]]) for i in ids))
    return evaluate(out, {}, {}, None, {})["frames"]


def test_student_strategies_and_cycles_are_parsed():
    from experiments.student import parse_cycles, parse_strategies

    s = parse_strategies("none, auto@0.85,hybrid@0.7,manual")
    assert [(x.kind, x.threshold) for x in s] == [("none", 0.0), ("auto", 0.85), ("hybrid", 0.7), ("manual", 0.0)]
    c = parse_cycles(["a,b>c", "a,b,c>d,e"])
    assert c[1].index == 2 and c[1].train == ("a", "b", "c") and c[1].test == ("d", "e")
    for bad in (["a,b"], ["a>a"], [">c"]):
        with pytest.raises(SystemExit):
            parse_cycles(bad)
    with pytest.raises(SystemExit):
        parse_strategies("auto")


def test_student_group_can_come_from_file_name():
    import re
    from experiments.student import group_of, stem_of

    pattern = re.compile(r"_(2024\d{4})\d{6}_")
    assert group_of("train/Placa_Dianteira_20240319004552_x.jpg", "brazil", pattern) == "20240319"
    assert group_of("train/other.jpg", "brazil", pattern) == ""
    assert group_of("2025-11-09/images/f.jpg", "2025-11-09", None) == "2025-11-09"
    assert stem_of("2025-11-09/images/frame_1-2.jpg") == "2025-11-09__images__frame_1-2"


def test_student_training_set_follows_the_strategy(tmp_path):
    from experiments.student import Strategy, assemble

    day1 = [f for f in _student_frames(tmp_path) if f["item"].domain == "d1"]
    none = assemble(day1, Strategy("none", "none"))
    assert (len(none.samples), none.human, none.dropped, none.label_accuracy) == (10, 0, 0, 0.6)
    auto = assemble(day1, Strategy("auto@0.8", "auto", 0.8))
    assert (len(auto.samples), auto.human, auto.dropped, auto.label_accuracy) == (6, 0, 4, 1.0)
    hybrid = assemble(day1, Strategy("hybrid@0.8", "hybrid", 0.8))
    assert (len(hybrid.samples), hybrid.human, hybrid.dropped, hybrid.label_accuracy) == (10, 4, 0, 1.0)
    assert all(len(s.boxes) == 1 for s in hybrid.samples)          # спорные кадры получили эталонную рамку
    manual = assemble(day1, Strategy("manual", "manual"))
    assert (manual.human, manual.label_accuracy) == (10, 1.0)


def test_student_experiment_runs_every_pair_once_and_resumes(tmp_path):
    from experiments.student import parse_cycles, parse_strategies, run_experiment, write_report

    frames = _student_frames(tmp_path)
    groups = {f["item"].frame_id: f["item"].domain for f in frames}
    calls = []

    def fake_train(job, samples, start, epochs, imgsz, seed):
        calls.append((job.name, len(samples), start))
        return "weights-of-" + job.name

    fake_eval = lambda job, weights, samples, imgsz: {"map50": len(samples) / 100, "map50_95": 0.1, "precision": 0.5, "recall": 0.5}
    report = tmp_path / "report"
    report.mkdir()
    args = (frames, groups, parse_cycles(["d1>d2"]), parse_strategies("none,auto@0.8,hybrid@0.8,manual"),
            report / "results.jsonl", tmp_path / "work", 1, 64, 0, "fresh", "yolov8n.pt")
    rows = run_experiment(*args, train_fn=fake_train, eval_fn=fake_eval)
    assert [c[1] for c in calls] == [10, 6, 10, 10] and all(c[2] == "yolov8n.pt" for c in calls)
    assert [r["human_fraction"] for r in rows] == [0.0, 0.0, 0.4, 1.0]
    assert all(r["test_frames"] == 10 and r["map50"] == 0.1 for r in rows)

    again = run_experiment(*args, train_fn=fake_train, eval_fn=fake_eval)     # всё из кэша, обучение не вызывается
    assert len(calls) == 4 and again == rows
    write_report(rows, report, ["заголовок"])
    summary = (report / "summary.md").read_text(encoding="utf-8")
    assert "hybrid@0.8" in summary and "40.0%" in summary and (report / "results.csv").is_file()

    with pytest.raises(SystemExit):
        run_experiment(frames, groups, parse_cycles(["d1>d9"]), parse_strategies("none"), tmp_path / "r.jsonl",
                       tmp_path / "w", 1, 64, 0, "fresh", "yolov8n.pt", train_fn=fake_train, eval_fn=fake_eval)
