import pytest

from quorum_worker.common import box_from_xyxy, clean_vlm_answer
from quorum_worker.teachers import TeacherError, resolve
from quorum_worker.teachers.detectors import GroundingDinoTeacher, Owlv2Teacher, YoloWorldTeacher
from quorum_worker.teachers.vlm import VlmTeacher


@pytest.mark.parametrize("raw,expected", [
    ("А755АХ44", "A755AX44"),
    ("The plate reads: A755AX44.", "A755AX44"),
    ("<think>hmm, maybe</think> X961HA96", "X961HA96"),
    ("a 755 ax 44", "A755AX44"),
    ("A755-AX-44", "A755AX44"),
    ("C622KA194", "C622KA194"),
    # Отказы: без цифр это не номер — иначе «НЕТ НОМЕРА» стало бы меткой HETHOMEPA.
    ("нет номера", ""),
    ("НЕТ НОМЕРА", ""),
    ("NONE", ""),
    ("I cannot read the plate", ""),
    ("", ""),
    (None, ""),
    # Путаница 0/O по позиции и «RUS» с флажка.
    ("A1980077", "A198OO77"),
    ("A2780092RUS", "A278OO92"),
    ("A278OO92 RUS", "A278OO92"),
    ("0123AB77", "O123AB77"),
    ("Номер: B9780077 RUS", "B978OO77"),
    # Не российский формат, но похоже на номер.
    ("B-MW 1234", "BMW1234"),
])
def test_vlm_answers_are_cleaned(raw, expected):
    assert clean_vlm_answer(raw) == expected


def test_box_from_pixels_is_normalized_and_clipped():
    b = box_from_xyxy(-5, 10, 110, 60, 100, 100, 0.8)
    assert (b.x, b.y, b.w, b.h, b.score) == (0.0, 0.1, 1.0, 0.5, 0.8)
    assert box_from_xyxy(10, 10, 10, 40, 100, 100, 0.9) is None  # вырожденный


@pytest.mark.parametrize("name,cls,model", [
    ("grounding-dino-swin-b-1234", GroundingDinoTeacher, "IDEA-Research/grounding-dino-base"),
    ("grounding-dino-tiny", GroundingDinoTeacher, "IDEA-Research/grounding-dino-tiny"),
    ("mm-grounding-dino-large", GroundingDinoTeacher, "openmmlab-community/mm_grounding_dino_large_all"),
    ("mm-grounding-dino", GroundingDinoTeacher, "openmmlab-community/mm_grounding_dino_large_all"),
    ("mm-grounding-dino-base-7", GroundingDinoTeacher, "openmmlab-community/mm_grounding_dino_base_all"),
    ("llmdet-large", GroundingDinoTeacher, "iSEE-Laboratory/llmdet_large"),
    ("owlv2-large", Owlv2Teacher, "google/owlv2-large-patch14-ensemble"),
    ("owlv2-base-77", Owlv2Teacher, "google/owlv2-base-patch16-ensemble"),
])
def test_open_vocab_teachers_resolve_by_prefix(name, cls, model):
    t = resolve(name, "OpenVocabDetector")
    assert isinstance(t, cls) and t.model_key == model


def test_yolo_world_size_from_name():
    assert resolve("yolo-world-l-1234", "OpenVocabDetector").weights == "yolov8l-worldv2.pt"
    assert resolve("yolo-world-s", "OpenVocabDetector").weights == "yolov8s-worldv2.pt"
    assert isinstance(resolve("yolo-world", "OpenVocabDetector"), YoloWorldTeacher)


def test_unknown_teachers_fail_fast():
    with pytest.raises(TeacherError):
        resolve("faster-rcnn", "OpenVocabDetector")
    with pytest.raises(TeacherError):
        resolve("no-such-weights", "Detector")


def test_vlm_model_name_drops_demo_suffix(tmp_path, monkeypatch):
    assert VlmTeacher("qwen3-vl-8b-1234").model == "qwen3-vl-8b"
    mapping = tmp_path / "map.json"
    mapping.write_text('{"qwen": "Qwen3-VL-8B-Instruct-Q4_K_M"}')
    monkeypatch.setenv("TEACHER_MAP_FILE", str(mapping))
    assert VlmTeacher("qwen").model == "Qwen3-VL-8B-Instruct-Q4_K_M"


def test_dataset_loader_skips_macos_junk_and_reads_roboflow(tmp_path):
    import json
    from PIL import Image
    from experiments.datasets import load_coco, load_ocr

    for split in ("train", "valid"):
        d = tmp_path / split
        d.mkdir()
        Image.new("RGB", (100, 50)).save(d / "a.jpg")
        (d / "_annotations.coco.json").write_text(json.dumps({
            "images": [{"id": 1, "file_name": "a.jpg", "width": 100, "height": 50}],
            "annotations": [{"id": 1, "image_id": 1, "category_id": 1, "bbox": [10, 5, 20, 10]}]}))
        (d / "._annotations.coco.json").write_bytes(b"\x00\x05\x16\x07Mac OS X\xb0\xff")
    items = load_coco(tmp_path)
    assert sorted(i.frame_id for i in items) == ["train/a.jpg", "valid/a.jpg"]
    assert items[0].boxes[0].x == 0.1 and items[0].boxes[0].h == 0.2

    ocr = tmp_path / "ocr"
    ocr.mkdir()
    Image.new("RGB", (40, 10)).save(ocr / "A123BC77___40___10.png")
    (ocr / "._A123BC77___40___10.png").write_bytes(b"\x00\x05\x16\x07")
    assert [i.text for i in load_ocr(ocr)] == ["A123BC77"]


@pytest.mark.parametrize("name,expected", [
    ("A090XM44_0.png", "A090XM44"),
    ("A159PA199_12.png", "A159PA199"),
    ("A278OO92.png", "A278OO92"),
    ("A123BC77___40___10.png", "A123BC77"),
])
def test_duplicate_suffix_is_not_part_of_plate(name, expected):
    from experiments.datasets import text_from_name
    assert text_from_name(name) == expected


def test_max_box_area_drops_whole_car_boxes(monkeypatch):
    from quorum_worker.common import Box
    from quorum_worker.teachers.detectors import _limit, _max_area

    car, plate = Box(0.1, 0.1, 0.8, 0.7, 0.6), Box(0.4, 0.7, 0.15, 0.05, 0.35)
    assert _limit([car, plate], 1.0) == [car, plate]
    assert _limit([car, plate], 0.1) == [plate]
    monkeypatch.setenv("DETECTOR_MAX_BOX_AREA", "0.2")
    monkeypatch.setenv("GDINO_MAX_BOX_AREA", "0.1")
    assert _max_area("GDINO_MAX_BOX_AREA") == 0.1 and _max_area("OWLV2_MAX_BOX_AREA") == 0.2


@pytest.mark.parametrize("name,kind", [
    ("mm-grounding-dino-large", "OpenVocabDetector"),
    ("llmdet-large", "OpenVocabDetector"),
    ("owlv2-large", "OpenVocabDetector"),
    ("yolov8l-plates", "Detector"),
])
def test_experiments_pick_teacher_kind_by_name(name, kind):
    from experiments.run import kind_for
    assert kind_for("detection", name) == kind
