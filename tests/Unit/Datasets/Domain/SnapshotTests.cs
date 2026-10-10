using Datasets.Domain.Frames;
using Datasets.Domain.Snapshots;
using Quorum.Domain;

namespace Datasets.UnitTests.Domain;

public class SnapshotTests
{
    [Fact]
    public void Every_fifth_frame_goes_to_holdout()
    {
        var camera = TestData.Camera(minFrames: 10);
        var frames = TestData.Frames(camera, 10);

        var snapshot = DatasetSnapshot.Create(camera, Guid.NewGuid(), frames.Select(f => (f.Id, f.StoragePath)).ToList());

        Assert.Equal(8, snapshot.TrainCount);
        Assert.Equal(2, snapshot.HoldoutCount);
        Assert.Equal(DataSplit.Holdout, snapshot.Frames[4].Split);
        Assert.Equal(SnapshotStatus.Locked, snapshot.Status);
    }

    [Fact]
    public void Snapshot_needs_minimum_frames()
    {
        var camera = TestData.Camera(minFrames: 5);
        var frames = TestData.Frames(camera, 4);
        Assert.Throws<DomainException>(() =>
            DatasetSnapshot.Create(camera, Guid.NewGuid(), frames.Select(f => (f.Id, f.StoragePath)).ToList()));
    }

    [Fact]
    public void Released_snapshot_cannot_be_released_again()
    {
        var camera = TestData.Camera(minFrames: 1);
        var frames = TestData.Frames(camera, 1);
        var snapshot = DatasetSnapshot.Create(camera, Guid.NewGuid(), frames.Select(f => (f.Id, f.StoragePath)).ToList());

        snapshot.MarkUsed();

        Assert.Throws<DomainException>(snapshot.Discard);
    }

    [Fact]
    public void Frame_belongs_to_one_snapshot_and_returns_on_detach()
    {
        var camera = TestData.Camera();
        var frame = TestData.Frames(camera, 1)[0];
        var first = Guid.NewGuid();

        frame.AttachTo(first);
        Assert.Throws<DomainException>(() => frame.AttachTo(Guid.NewGuid()));

        frame.Detach(first);
        Assert.Null(frame.SnapshotId);
    }

    [Fact]
    public void Unsupported_content_type_is_rejected() =>
        Assert.Throws<DomainException>(() => Frame.Create(Guid.NewGuid(), Guid.NewGuid(), "a/b.gif", "image/gif", 10, null));
}
