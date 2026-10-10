using Datasets.Application.Abstractions;
using Datasets.Domain.Snapshots;
using Datasets.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace Datasets.Infrastructure.Repositories;

internal sealed class SnapshotRepository(DatasetsDbContext db) : ISnapshotRepository
{
    public Task<DatasetSnapshot?> GetAsync(Guid id, CancellationToken ct) => db.Snapshots.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<DatasetSnapshot?> GetByCycleAsync(Guid cycleId, CancellationToken ct) =>
        db.Snapshots.FirstOrDefaultAsync(s => s.CycleId == cycleId, ct);

    public Task<bool> HasLockedAsync(Guid cameraId, CancellationToken ct) =>
        db.Snapshots.AnyAsync(s => s.CameraId == cameraId && s.Status == SnapshotStatus.Locked, ct);

    public void Add(DatasetSnapshot snapshot) => db.Snapshots.Add(snapshot);
}
