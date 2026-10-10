using Labeling.Application.Abstractions;
using Labeling.Application.Features.Runs.ProcessLabelingRun;
using Labeling.Application.Features.Runs.StartLabelingRun;
using Labeling.Domain.Common;
using Labeling.Domain.Consensus;
using Labeling.Domain.Labels;
using Labeling.Domain.Reviews;
using Labeling.Domain.Runs;
using Labeling.Domain.Teachers;
using NSubstitute;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;

namespace Labeling.UnitTests.Features;

public class LabelingHandlerTests
{
    private readonly ITeacherRepository _teachers = Substitute.For<ITeacherRepository>();
    private readonly IRunRepository _runs = Substitute.For<IRunRepository>();
    private readonly ILabelRepository _labels = Substitute.For<ILabelRepository>();
    private readonly IReviewTaskRepository _reviews = Substitute.For<IReviewTaskRepository>();
    private readonly IFrameSource _frames = Substitute.For<IFrameSource>();
    private readonly IIntegrationEventPublisher _events = Substitute.For<IIntegrationEventPublisher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly InMemoryPredictions _predictions = new();

    [Fact]
    public async Task No_active_teachers_fails_the_step_instead_of_throwing()
    {
        _teachers.ListAsync(TaskType.Detection, true, Arg.Any<CancellationToken>()).Returns(new List<TeacherModel>());
        var cycleId = Guid.NewGuid();

        await new StartLabelingRunHandler(_teachers, _runs, _events, _uow).Handle(
            new StartLabelingRunCommand(cycleId, Guid.NewGuid(), "Detection", "license plate", "Hybrid", 0.7), CancellationToken.None);

        _runs.DidNotReceiveWithAnyArgs().Add(default!);
        await _events.Received(1).PublishAsync(Arg.Is<LabelingFailed>(e => e.CycleId == cycleId), Arg.Any<CancellationToken>());
    }

    private LabelingRun ArrangeRun(ValidationMode mode, params SourceFrame[] frames)
    {
        var run = LabelingRun.Start(Guid.NewGuid(), Guid.NewGuid(), TaskType.Detection, "license plate",
            ValidationStrategy.Create(mode, 0.7),
            [TeacherModel.Register("a", TeacherKind.OpenVocabDetector, TaskType.Detection, 1),
             TeacherModel.Register("b", TeacherKind.OpenVocabDetector, TaskType.Detection, 1)]);
        _runs.GetAsync(run.Id, Arg.Any<CancellationToken>()).Returns(run);
        _frames.GetSnapshotFramesAsync(run.SnapshotId, Arg.Any<CancellationToken>()).Returns(frames.ToList());
        return run;
    }

    private ProcessLabelingRunHandler Process(ITeacherRunner runner) =>
        new(_runs, _predictions, _labels, _reviews, _frames, runner, _events, _uow);

    [Fact]
    public async Task Agreed_frames_are_accepted_and_disputed_go_to_review()
    {
        var easy = new SourceFrame(Guid.NewGuid(), "easy.jpg", DataSplit.Train);
        var hard = new SourceFrame(Guid.NewGuid(), "hard.jpg", DataSplit.Holdout);
        var run = ArrangeRun(ValidationMode.Hybrid, easy, hard);
        var plate = new BoundingBox(0.1, 0.1, 0.2, 0.1, 0.9);
        // На «трудном» кадре учитель b ничего не видит → согласие 0.5 < 0.7 → человеку.
        var runner = new FakeTeacherRunner((t, f) => new TeacherPrediction(f.FrameId,
            f == hard && t.Name == "b" ? [] : [plate], null, 0.9));

        await Process(runner).Handle(new ProcessLabelingRunCommand(run.Id), CancellationToken.None);

        Assert.Equal(4, _predictions.Items.Count); // кэш: 2 учителя × 2 кадра
        Assert.Equal(RunStatus.Reviewing, run.Status);
        Assert.Equal((1, 1), (run.Accepted, run.PendingReview));
        _labels.Received(1).Add(Arg.Is<ConsensusLabel>(l => l.FrameId == easy.FrameId && l.Status == LabelStatus.Accepted));
        _reviews.Received(1).Add(Arg.Is<ReviewTask>(t => t.FrameId == hard.FrameId));
        await _events.DidNotReceiveWithAnyArgs().PublishAsync<LabelingCompleted>(default!, default);
    }

    [Fact]
    public async Task Auto_mode_completes_immediately()
    {
        var frame = new SourceFrame(Guid.NewGuid(), "f.jpg", DataSplit.Train);
        var run = ArrangeRun(ValidationMode.Auto, frame);
        var runner = new FakeTeacherRunner((_, f) => new TeacherPrediction(f.FrameId, [new BoundingBox(0.1, 0.1, 0.2, 0.1)], null, 1));

        await Process(runner).Handle(new ProcessLabelingRunCommand(run.Id), CancellationToken.None);

        Assert.Equal(RunStatus.Completed, run.Status);
        await _events.Received(1).PublishAsync(
            Arg.Is<LabelingCompleted>(e => e.RunId == run.Id && e.Accepted == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Worker_outage_fails_the_run_and_notifies_the_saga()
    {
        var run = ArrangeRun(ValidationMode.Auto, new SourceFrame(Guid.NewGuid(), "f.jpg", DataSplit.Train));

        await Process(new FakeTeacherRunner(null)).Handle(new ProcessLabelingRunCommand(run.Id), CancellationToken.None);

        Assert.Equal(RunStatus.Failed, run.Status);
        await _events.Received(1).PublishAsync(
            Arg.Is<LabelingFailed>(e => e.RunId == run.Id && e.Reason.Contains("worker is down")), Arg.Any<CancellationToken>());
    }
}
