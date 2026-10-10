using Datasets.Domain.Snapshots;

namespace Datasets.Application.Models;

public sealed record SnapshotFrameDto(Guid FrameId, string StoragePath, string Split);

public sealed record SnapshotDto(Guid Id, Guid CameraId, Guid CycleId, string Task, string Status,
    int TrainFrames, int HoldoutFrames, DateTime CreatedAt, DateTime? ReleasedAt)
{
    public static SnapshotDto From(DatasetSnapshot s) => new(
        s.Id, s.CameraId, s.CycleId, s.Task.ToString(), s.Status.ToString(),
        s.TrainCount, s.HoldoutCount, s.CreatedAt, s.ReleasedAt);
}
