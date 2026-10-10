using Quorum.Domain;
using Training.Domain.Common;
using Training.Domain.Cycles;

namespace Training.UnitTests.Domain;

public class AdaptationCycleTests
{
    [Fact]
    public void Happy_path_goes_through_all_steps()
    {
        var cycle = TestData.CycleIn(CycleStatus.Evaluating);
        cycle.Complete(promoted: true);

        Assert.Equal(CycleStatus.Completed, cycle.Status);
        Assert.Equal(CycleOutcome.Promoted, cycle.Outcome);
        Assert.NotNull(cycle.FinishedAt);
    }

    [Fact]
    public void Rejected_snapshot_skips_cycle_without_compensations()
    {
        var cycle = TestData.CycleIn(CycleStatus.Snapshotting);
        cycle.Skip("Not enough new frames");
        Assert.Equal(CycleStatus.Skipped, cycle.Status);
        Assert.True(cycle.IsFinished);
    }

    [Fact]
    public void Labeling_failure_releases_snapshot_only()
    {
        var compensation = TestData.CycleIn(CycleStatus.Labeling).OnLabelingFailed("worker is down");
        Assert.Equal(new CycleCompensation(DiscardLabeling: false, ReleaseSnapshot: true), compensation);
    }

    [Fact]
    public void Too_few_labels_fails_and_rolls_back_labeling_and_snapshot()
    {
        var cycle = TestData.CycleIn(CycleStatus.Labeling);

        var compensation = cycle.OnLabelingCompleted(Guid.NewGuid(), acceptedLabels: 3, minTrainingLabels: 10);

        Assert.Equal(new CycleCompensation(DiscardLabeling: true, ReleaseSnapshot: true), compensation);
        Assert.Equal(CycleStatus.Failed, cycle.Status);
        Assert.Contains("3 < 10", cycle.FailureReason);
    }

    [Fact]
    public void Training_failure_rolls_back_everything()
    {
        var compensation = TestData.CycleIn(CycleStatus.Training).OnTrainingFailed("GPU");
        Assert.Equal(new CycleCompensation(true, true), compensation);
    }

    [Fact]
    public void Out_of_order_event_is_a_domain_error()
    {
        var cycle = TestData.CycleIn(CycleStatus.Snapshotting);
        Assert.Throws<DomainException>(() => cycle.OnTrainingSucceeded(Guid.NewGuid()));
        Assert.Throws<DomainException>(() => TestData.CycleIn(CycleStatus.Labeling).OnSnapshotCreated(Guid.NewGuid(), TaskType.Ocr, 1, 0));
    }
}
