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
