using Datasets.Application.Abstractions;
using Datasets.Application.Features.Snapshots.CreateSnapshot;
using Datasets.Application.Features.Snapshots.ReleaseSnapshot;
using Datasets.Domain.Snapshots;
using NSubstitute;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;

namespace Datasets.UnitTests.Features;

public class SnapshotHandlerTests
{
    private readonly ICameraRepository _cameras = Substitute.For<ICameraRepository>();
    private readonly IFrameRepository _frames = Substitute.For<IFrameRepository>();
    private readonly ISnapshotRepository _snapshots = Substitute.For<ISnapshotRepository>();
    private readonly IIntegrationEventPublisher _events = Substitute.For<IIntegrationEventPublisher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private CreateSnapshotHandler Create() => new(_cameras, _frames, _snapshots, _events, _uow);

    [Fact]
    public async Task Not_enough_new_frames_rejects_snapshot()
    {
        var camera = TestData.Camera(minFrames: 10);
        _cameras.GetAsync(camera.Id, Arg.Any<CancellationToken>()).Returns(camera);
        _frames.TakeNewAsync(camera.Id, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(TestData.Frames(camera, 3));
        var cycleId = Guid.NewGuid();

        await Create().Handle(new CreateSnapshotCommand(cycleId, camera.Id), CancellationToken.None);

        _snapshots.DidNotReceiveWithAnyArgs().Add(default!);
        await _events.Received(1).PublishAsync(
            Arg.Is<SnapshotRejected>(e => e.CycleId == cycleId && e.Reason.Contains("3 < 10")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Second_cycle_on_busy_camera_is_rejected()
    {
        var camera = TestData.Camera(minFrames: 1);
        _cameras.GetAsync(camera.Id, Arg.Any<CancellationToken>()).Returns(camera);
        _snapshots.HasLockedAsync(camera.Id, Arg.Any<CancellationToken>()).Returns(true);

        await Create().Handle(new CreateSnapshotCommand(Guid.NewGuid(), camera.Id), CancellationToken.None);

        await _events.Received(1).PublishAsync(Arg.Any<SnapshotRejected>(), Arg.Any<CancellationToken>());
        await _frames.DidNotReceiveWithAnyArgs().TakeNewAsync(default, default, default);
    }

    [Fact]
    public async Task Snapshot_takes_new_frames_and_reports_split()
    {
        var camera = TestData.Camera(minFrames: 10);
        var frames = TestData.Frames(camera, 10);
        _cameras.GetAsync(camera.Id, Arg.Any<CancellationToken>()).Returns(camera);
        _frames.TakeNewAsync(camera.Id, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(frames);
        DatasetSnapshot? saved = null;
        _snapshots.Add(Arg.Do<DatasetSnapshot>(s => saved = s));

        await Create().Handle(new CreateSnapshotCommand(Guid.NewGuid(), camera.Id), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.All(frames, f => Assert.Equal(saved!.Id, f.SnapshotId));
        await _events.Received(1).PublishAsync(
            Arg.Is<SnapshotCreated>(e => e.SnapshotId == saved!.Id && e.TrainFrames == 8 && e.HoldoutFrames == 2 && e.Task == "Detection"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Discarding_snapshot_returns_frames_to_new()
    {
        var camera = TestData.Camera(minFrames: 2);
        var frames = TestData.Frames(camera, 2);
        var snapshot = DatasetSnapshot.Create(camera, Guid.NewGuid(), frames.Select(f => (f.Id, f.StoragePath)).ToList());
        foreach (var f in frames) f.AttachTo(snapshot.Id);
        _snapshots.GetAsync(snapshot.Id, Arg.Any<CancellationToken>()).Returns(snapshot);
        _frames.ListBySnapshotAsync(snapshot.Id, Arg.Any<CancellationToken>()).Returns(frames);

        await new ReleaseSnapshotHandler(_frames, _snapshots, _events, _uow)
            .Handle(new ReleaseSnapshotCommand(snapshot.CycleId, snapshot.Id, Discard: true), CancellationToken.None);

        Assert.Equal(SnapshotStatus.Discarded, snapshot.Status);
        Assert.All(frames, f => Assert.Null(f.SnapshotId));
        await _events.Received(1).PublishAsync(Arg.Is<SnapshotReleased>(e => e.Discarded), Arg.Any<CancellationToken>());
    }
}
