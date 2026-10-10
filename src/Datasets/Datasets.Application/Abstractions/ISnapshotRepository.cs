using Datasets.Domain.Snapshots;

namespace Datasets.Application.Abstractions;

public interface ISnapshotRepository
{
    Task<DatasetSnapshot?> GetAsync(Guid id, CancellationToken ct);
    Task<DatasetSnapshot?> GetByCycleAsync(Guid cycleId, CancellationToken ct);
    Task<bool> HasLockedAsync(Guid cameraId, CancellationToken ct);
    void Add(DatasetSnapshot snapshot);
}
