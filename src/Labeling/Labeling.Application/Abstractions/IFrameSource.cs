using Labeling.Domain.Common;

namespace Labeling.Application.Abstractions;

public sealed record SourceFrame(Guid FrameId, string Path, DataSplit Split);

/// <summary>Порт к Datasets: состав снимка. Бросает ExternalServiceException, если Datasets недоступен.</summary>
public interface IFrameSource
{
    Task<IReadOnlyList<SourceFrame>> GetSnapshotFramesAsync(Guid snapshotId, CancellationToken ct);
}
