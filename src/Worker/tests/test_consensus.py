"""
Те же случаи, что tests/Unit/Labeling/Domain/BoxConsensusTests.cs и TextConsensusTests.cs.
Если тест здесь падает, а в C# проходит — консенсус в экспериментах разошёлся с платформой.
"""
import pytest

from quorum_worker.common import Box, normalize_text
from quorum_worker.consensus import Vote, box_consensus, fleiss_kappa, text_consensus


def plate(x=0.1, score=0.9):
    return Box(x, 0.1, 0.2, 0.1, score)


def vote(t, w, *boxes):
    return Vote(t, w, list(boxes))


def test_unanimous_teachers_give_one_object_with_full_agreement():
    r = box_consensus([vote("a", 1, plate()), vote("b", 1, plate()), vote("c", 1, plate())])
    assert len(r.boxes) == 1 and r.agreement == 1.0 and r.boxes[0].score == 1.0


def test_lonely_extra_box_is_dropped_and_lowers_agreement():
    extra = Box(0.7, 0.7, 0.1, 0.1)
    r = box_consensus([vote("a", 1, plate(), extra), vote("b", 1, plate()), vote("c", 1, plate())])
    assert len(r.boxes) == 1 and r.agreement == 0.8333


def test_two_of_three_reach_quorum():
    r = box_consensus([vote("a", 1, plate()), vote("b", 1, plate()), vote("c", 1)])
    assert len(r.boxes) == 1 and r.agreement == 0.6667


def test_one_of_three_does_not_reach_quorum():
    r = box_consensus([vote("a", 1, plate()), vote("b", 1), vote("c", 1)])
    assert r.boxes == [] and r.agreement == 0.6667


def test_heavy_teacher_can_win_alone():
    assert len(box_consensus([vote("a", 3, plate()), vote("b", 1), vote("c", 1)]).boxes) == 1


def test_one_teacher_cannot_vote_twice_for_the_same_object():
    assert box_consensus([vote("a", 1, plate(0.10), plate(0.11)), vote("b", 1), vote("c", 1)]).boxes == []


def test_nobody_sees_anything_means_full_agreement():
    r = box_consensus([vote("a", 1), vote("b", 1)])
    assert r.boxes == [] and r.agreement == 1.0


def test_fused_box_is_weighted_average():
    r = box_consensus([vote("a", 1, plate(0.10, 1.0)), vote("b", 1, plate(0.12, 1.0))])
    assert r.boxes[0].x == pytest.approx(0.11) and r.boxes[0].w == pytest.approx(0.2)


@pytest.mark.parametrize("raw,expected", [("а123вс 77", "A123BC77"), ("A 123-BC|77", "A123BC77"), ("  ", "")])
def test_text_is_normalized(raw, expected):
    assert normalize_text(raw) == expected



def test_cyrillic_and_latin_answers_count_as_one_vote():
    r = text_consensus([Vote("a", 1, [], "A123BC77"), Vote("b", 1, [], "А123ВС77"), Vote("c", 1, [], "A123BC71")])
    assert r.text == "A123BC77" and r.agreement == 0.6667


def test_unreadable_is_a_vote_too():
    r = text_consensus([Vote("a", 1, [], ""), Vote("b", 1, [], None), Vote("c", 1, [], "X1")])
    assert r.text is None and r.agreement == 0.6667


def test_on_a_tie_text_beats_unreadable():
    r = text_consensus([Vote("a", 1, [], "AB12"), Vote("b", 1, [], "")])
    assert r.text == "AB12" and r.agreement == 0.5


def test_weights_matter():
    r = text_consensus([Vote("a", 3, [], "B777OP"), Vote("b", 1, [], "B771OP"), Vote("c", 1, [], "B771OP")])
    assert r.text == "B777OP" and r.agreement == 0.6


def test_fleiss_kappa_perfect_and_chance():
    assert fleiss_kappa([["A", "A", "A"], ["B", "B", "B"]]) == pytest.approx(1.0)
    # Классический пример: согласие на уровне случайного даёт каппу около нуля или ниже.
    assert fleiss_kappa([["A", "B"], ["B", "A"], ["A", "B"], ["B", "A"]]) < 0
