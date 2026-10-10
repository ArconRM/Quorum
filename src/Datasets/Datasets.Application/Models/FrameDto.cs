using Datasets.Domain.Frames;

namespace Datasets.Application.Models;

public sealed record FrameDto(Guid Id, Guid CameraId, string StoragePath, string ContentType, long SizeBytes,
    DateTime CapturedAt, DateTime UploadedAt, Guid? SnapshotId)
{
    public static FrameDto From(Frame f) =>
        new(f.Id, f.CameraId, f.StoragePath, f.ContentType, f.SizeBytes, f.CapturedAt, f.UploadedAt, f.SnapshotId);
}
