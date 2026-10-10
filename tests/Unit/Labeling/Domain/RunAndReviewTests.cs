using Labeling.Domain.Common;
using Labeling.Domain.Consensus;
using Labeling.Domain.Labels;
using Labeling.Domain.Reviews;
using Labeling.Domain.Runs;
using Labeling.Domain.Teachers;
using Quorum.Domain;

namespace Labeling.UnitTests.Domain;

public class RunAndReviewTests
{
    private static TeacherModel Detector(string name = "owlv2") => TeacherModel.Register(name, TeacherKind.OpenVocabDetector, TaskType.Detection, 1);

    private static LabelingRun NewRun(ValidationMode mode = ValidationMode.Hybrid, int frames = 3)
    {
        var run = LabelingRun.Start(Guid.NewGuid(), Guid.NewGuid(), TaskType.Detection, "license plate",
            ValidationStrategy.Create(mode, 0.7), [Detector("a"), Detector("b")]);
        run.SetTotalFrames(frames);
        foreach (var t in run.Teachers) run.CompleteTeacher(t.TeacherId);
        return run;
    }

    [Theory]
    [InlineData(ValidationMode.Auto, 0.69, LabelStatus.Rejected)]
    [InlineData(ValidationMode.Auto, 0.70, LabelStatus.Accepted)]
    [InlineData(ValidationMode.Hybrid, 0.69, LabelStatus.NeedsReview)]
    [InlineData(ValidationMode.Hybrid, 0.95, LabelStatus.Accepted)]
    [InlineData(ValidationMode.Manual, 1.00, LabelStatus.NeedsReview)]
    public void Strategy_decides_label_status(ValidationMode mode, double agreement, LabelStatus expected) =>
        Assert.Equal(expected, ValidationStrategy.Create(mode, 0.7).Decide(agreement));

    [Fact]
    public void Vlm_cannot_label_boxes() =>
        Assert.Throws<DomainException>(() => TeacherModel.Register("qwen", TeacherKind.Vlm, TaskType.Detection, 1));

    [Fact]
    public void Run_requires_teachers_of_its_task()
    {
        var ocr = TeacherModel.Register("qwen", TeacherKind.Vlm, TaskType.Ocr, 1);
        Assert.Throws<DomainException>(() => LabelingRun.Start(Guid.NewGuid(), Guid.NewGuid(), TaskType.Detection, "p",
            ValidationStrategy.Create(ValidationMode.Auto, 0.7), [ocr]));
    }

    [Fact]
    public void Run_without_disputes_completes_right_after_aggregation()
    {
        var run = NewRun();
        run.FinishAggregation(accepted: 2, rejected: 1, needsReview: 0);
        Assert.Equal(RunStatus.Completed, run.Status);
    }

    [Fact]
    public void Last_review_completes_the_run_exactly_once()
    {
        var run = NewRun();
        run.FinishAggregation(accepted: 1, rejected: 0, needsReview: 2);
        Assert.Equal(RunStatus.Reviewing, run.Status);

        Assert.False(run.RecordReview(LabelStatus.Accepted));
        Assert.True(run.RecordReview(LabelStatus.Rejected));

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal((2, 1, 2), (run.Accepted, run.Rejected, run.Reviewed));
        Assert.Throws<DomainException>(() => run.RecordReview(LabelStatus.Accepted));
    }

    [Fact]
    public void Every_frame_must_get_a_label() =>
        Assert.Throws<DomainException>(() => NewRun(frames: 3).FinishAggregation(1, 1, 0));

    [Fact]
    public void Review_task_cannot_be_taken_from_another_annotator_until_it_goes_stale()
    {
        var label = ConsensusLabel.Create(Guid.NewGuid(), Guid.NewGuid(), "p.jpg", DataSplit.Train, TaskType.Detection,
            new ConsensusResult([], null, 0.5), LabelStatus.NeedsReview);
        var task = ReviewTask.For(label);
        var now = DateTime.UtcNow;
        var ann = Guid.NewGuid();
        var other = Guid.NewGuid();

        task.Claim(ann, now);
        Assert.Throws<DomainException>(() => task.Claim(other, now.AddMinutes(5)));
        Assert.Throws<DomainException>(() => task.Complete(other, ReviewVerdict.Approve));

        task.Claim(other, now + ReviewTask.ClaimTimeout + TimeSpan.FromMinutes(1)); // брошено — забрал другой
        task.Complete(other, ReviewVerdict.Approve);
        Assert.Equal(ReviewTaskStatus.Done, task.Status);
    }

    [Fact]
    public void Corrected_text_is_normalized_and_marked_human()
    {
        var label = ConsensusLabel.Create(Guid.NewGuid(), Guid.NewGuid(), "p.jpg", DataSplit.Train, TaskType.Ocr,
            new ConsensusResult([], "A123BC77", 0.5), LabelStatus.NeedsReview);

        label.ApplyReview(Guid.NewGuid(), ReviewVerdict.Correct, text: "а 124 вс 77");

        Assert.Equal("A124BC77", label.Text);
        Assert.Equal(LabelSource.Human, label.Source);
        Assert.Equal(LabelStatus.Accepted, label.Status);
    }
}
