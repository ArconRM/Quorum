using Quorum.Domain;
using Training.Domain.Evaluations;
using Training.Domain.Models;

namespace Training.UnitTests.Domain;

public class ModelAndEvaluationTests
{
    [Fact]
    public void First_model_is_always_promoted()
    {
        var e = Evaluation.Decide(Guid.NewGuid(), Guid.NewGuid(), null, "mAP50", 0.6, null, 3, 0.01, 20);
        Assert.Equal(EvaluationDecision.Promote, e.Decision);
    }

    [Theory]
    [InlineData(0.80, 0.79, 0.00, EvaluationDecision.Promote)]
    [InlineData(0.80, 0.80, 0.00, EvaluationDecision.Reject)]
    [InlineData(0.80, 0.79, 0.02, EvaluationDecision.Reject)]
    [InlineData(0.70, 0.75, 0.00, EvaluationDecision.Reject)]
    public void Candidate_must_beat_baseline(double candidate, double baseline, double minImprovement, EvaluationDecision expected)
    {
        var e = Evaluation.Decide(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "mAP50", candidate, baseline, 20, minImprovement, 20);
        Assert.Equal(expected, e.Decision);
    }

    [Fact]
    public void Small_holdout_with_published_model_is_inconclusive()
    {
        var e = Evaluation.Decide(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "mAP50", 0.95, 0.60, 6, 0.0, 20);
        Assert.Equal(EvaluationDecision.Inconclusive, e.Decision);
    }

    [Fact]
    public void Tie_on_sufficient_holdout_is_rejected()
    {
        var e = Evaluation.Decide(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "mAP50", 0.964, 0.964, 20, 0.0, 20);
        Assert.Equal(EvaluationDecision.Reject, e.Decision);
    }

    [Fact]
    public void Better_candidate_on_sufficient_holdout_is_promoted()
    {
        var e = Evaluation.Decide(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "mAP50", 0.90, 0.85, 20, 0.0, 20);
        Assert.Equal(EvaluationDecision.Promote, e.Decision);
    }

    [Fact]
    public void Publishing_archives_the_current_version_and_rollback_restores_it()
    {
        var camera = Guid.NewGuid();
        var v1 = TestData.Version(camera, 1);
        var v2 = TestData.Version(camera, 2);

        ModelPublication.Publish(v1, null);
        ModelPublication.Publish(v2, v1);
        Assert.Equal((ModelVersionStatus.Archived, ModelVersionStatus.Published), (v1.Status, v2.Status));

        ModelPublication.Restore(v1, v2);
        Assert.Equal((ModelVersionStatus.Published, ModelVersionStatus.Archived), (v1.Status, v2.Status));
    }

    [Fact]
    public void Rejected_candidate_cannot_be_restored()
    {
        var v = TestData.Version(Guid.NewGuid(), 1);
        v.Reject();
        Assert.Throws<DomainException>(() => ModelPublication.Restore(v, null));
    }
}
