import json

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
