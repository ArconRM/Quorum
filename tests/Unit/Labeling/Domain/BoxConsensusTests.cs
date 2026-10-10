using Labeling.Domain.Consensus;

namespace Labeling.UnitTests.Domain;

public class BoxConsensusTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();

    private static BoundingBox Plate(double x = 0.1, double score = 0.9) => new(x, 0.1, 0.2, 0.1, score);

    private static TeacherVote Vote(Guid id, double weight, params BoundingBox[] boxes) => new(id, weight, boxes, null);

    [Fact]
    public void Unanimous_teachers_give_one_object_with_full_agreement()
    {
        var result = BoxConsensus.Compute([Vote(A, 1, Plate()), Vote(B, 1, Plate()), Vote(C, 1, Plate())]);

        var box = Assert.Single(result.Boxes);
        Assert.Equal(1.0, result.Agreement);
        Assert.Equal(1.0, box.Score); // score итогового бокса = поддержка
    }

    [Fact]
    public void Lonely_extra_box_is_dropped_and_lowers_agreement()
    {
        var extra = new BoundingBox(0.7, 0.7, 0.1, 0.1);
        var result = BoxConsensus.Compute([Vote(A, 1, Plate(), extra), Vote(B, 1, Plate()), Vote(C, 1, Plate())]);

        Assert.Single(result.Boxes);
        Assert.Equal(0.8333, result.Agreement); // (1 + 2/3) / 2
    }

    [Fact]
    public void Two_of_three_reach_quorum()
    {
        var result = BoxConsensus.Compute([Vote(A, 1, Plate()), Vote(B, 1, Plate()), Vote(C, 1)]);

        Assert.Single(result.Boxes);
        Assert.Equal(0.6667, result.Agreement);
    }

    [Fact]
    public void One_of_three_does_not_reach_quorum()
    {
        var result = BoxConsensus.Compute([Vote(A, 1, Plate()), Vote(B, 1), Vote(C, 1)]);

        Assert.Empty(result.Boxes);
        Assert.Equal(0.6667, result.Agreement);
    }

    [Fact]
    public void Heavy_teacher_can_win_alone()
    {
        var result = BoxConsensus.Compute([Vote(A, 3, Plate()), Vote(B, 1), Vote(C, 1)]);
        Assert.Single(result.Boxes); // 3 / 5 = 0.6 > 0.5
    }

    [Fact]
    public void One_teacher_cannot_vote_twice_for_the_same_object()
    {
        var result = BoxConsensus.Compute([Vote(A, 1, Plate(0.10), Plate(0.11)), Vote(B, 1), Vote(C, 1)]);
        Assert.Empty(result.Boxes);
    }

    [Fact]
    public void Nobody_sees_anything_means_full_agreement_on_empty_frame()
    {
        var result = BoxConsensus.Compute([Vote(A, 1), Vote(B, 1)]);
        Assert.Empty(result.Boxes);
        Assert.Equal(1.0, result.Agreement);
    }

    [Fact]
    public void Fused_box_is_weighted_average()
    {
        var result = BoxConsensus.Compute([Vote(A, 1, Plate(0.10, 1.0)), Vote(B, 1, Plate(0.12, 1.0))]);

        var box = Assert.Single(result.Boxes);
        Assert.Equal(0.11, box.X, 6);
        Assert.Equal(0.2, box.W, 6);
    }

    [Fact]
    public void IoU_of_identical_boxes_is_one_and_of_disjoint_is_zero()
    {
        Assert.Equal(1.0, Plate().IoU(Plate()), 6);
        Assert.Equal(0.0, Plate(0.1).IoU(Plate(0.6)), 6);
    }
}
