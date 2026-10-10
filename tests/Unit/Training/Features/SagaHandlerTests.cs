using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;
using Quorum.Application.Exceptions;
using Training.Application.Abstractions;
using Training.Application.Features.Cycles.OnLabelingCompleted;
using Training.Application.Features.Jobs.RunTrainingJob;
using Training.Application.Messages;
using Training.Domain.Cycles;
using Training.Domain.Jobs;

namespace Training.UnitTests.Features;

public class SagaHandlerTests
{
    private readonly ICycleRepository _cycles = Substitute.For<ICycleRepository>();
    private readonly ITrainingJobRepository _jobs = Substitute.For<ITrainingJobRepository>();
    private readonly IModelVersionRepository _versions = Substitute.For<IModelVersionRepository>();
    private readonly IAdaptationSettings _settings = Substitute.For<IAdaptationSettings>();
    private readonly IIntegrationEventPublisher _events = Substitute.For<IIntegrationEventPublisher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public SagaHandlerTests()
    {
        _settings.MinTrainingLabels.Returns(10);
        _settings.Epochs.Returns(5);
    }

    [Fact]
    public async Task Too_few_labels_sends_both_compensations()
    {
        var cycle = TestData.CycleIn(CycleStatus.Labeling);
        _cycles.GetAsync(cycle.Id, Arg.Any<CancellationToken>()).Returns(cycle);

        await new OnLabelingCompletedHandler(_cycles, _jobs, _versions, _settings, _events, _uow)
            .Handle(new OnLabelingCompletedCommand(cycle.Id, Guid.NewGuid(), Accepted: 2), CancellationToken.None);

        _jobs.DidNotReceiveWithAnyArgs().Add(default!);
        await _events.Received(1).PublishAsync(Arg.Is<DiscardLabeling>(c => c.CycleId == cycle.Id), Arg.Any<CancellationToken>());
        await _events.Received(1).PublishAsync(
            Arg.Is<ReleaseSnapshot>(c => c.SnapshotId == cycle.SnapshotId && c.Discard), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enough_labels_queues_training()
    {
        var cycle = TestData.CycleIn(CycleStatus.Labeling);
        _cycles.GetAsync(cycle.Id, Arg.Any<CancellationToken>()).Returns(cycle);
        TrainingJob? job = null;
        _jobs.Add(Arg.Do<TrainingJob>(j => job = j));

        await new OnLabelingCompletedHandler(_cycles, _jobs, _versions, _settings, _events, _uow)
            .Handle(new OnLabelingCompletedCommand(cycle.Id, Guid.NewGuid(), Accepted: 50), CancellationToken.None);

        Assert.Equal(CycleStatus.Training, cycle.Status);
        Assert.Equal("yolov8n", job!.Architecture);
        Assert.Equal(job.Id, cycle.TrainingJobId);
        await _events.Received(1).PublishAsync(Arg.Is<RunTrainingJob>(m => m.JobId == job.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Worker_outage_during_training_fails_cycle_with_compensations()
    {
        var cycle = TestData.CycleIn(CycleStatus.Training);
        var job = TrainingJob.Create(cycle.Id, cycle.CameraId, cycle.LabelingRunId!.Value, cycle.Task!.Value, 5, null, null);
        cycle.OnTrainingStarted(job.Id);
        _cycles.GetAsync(cycle.Id, Arg.Any<CancellationToken>()).Returns(cycle);
        _jobs.GetAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var data = Substitute.For<ILabeledDataSource>();
        data.GetAcceptedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<LabeledSample> { new(Guid.NewGuid(), "f.jpg", false, [], null) });
        var worker = Substitute.For<ITrainingWorker>();
        worker.TrainAsync(default!, default!, default!, default).ReturnsForAnyArgs(Task.FromException<TrainingResult>(new ExternalServiceException("GPU is gone")));

        await new RunTrainingJobHandler(_jobs, _cycles, _versions, data, worker, _events, _uow)
            .Handle(new RunTrainingJobCommand(job.Id), CancellationToken.None);

        Assert.Equal(TrainingJobStatus.Failed, job.Status);
        Assert.Equal(CycleStatus.Failed, cycle.Status);
        await _events.Received(1).PublishAsync(Arg.Any<DiscardLabeling>(), Arg.Any<CancellationToken>());
        await _events.Received(1).PublishAsync(Arg.Is<ReleaseSnapshot>(c => c.Discard), Arg.Any<CancellationToken>());
        _versions.DidNotReceiveWithAnyArgs().Add(default!);
    }
}
