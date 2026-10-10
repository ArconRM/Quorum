using Datasets.Application.Abstractions;
using Datasets.Domain.Frames;
using Datasets.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace Datasets.Infrastructure.Repositories;

internal sealed class FrameRepository(DatasetsDbContext db) : IFrameRepository
{
    public Task<Frame?> GetAsync(Guid id, CancellationToken ct) => db.Frames.FirstOrDefaultAsync(f => f.Id == id, ct);

    public Task<int> CountNewAsync(Guid cameraId, CancellationToken ct) =>
        db.Frames.CountAsync(f => f.CameraId == cameraId && f.SnapshotId == null, ct);

    public async Task<IReadOnlyList<Frame>> TakeNewAsync(Guid cameraId, int max, CancellationToken ct) =>
        await db.Frames
            .Where(f => f.CameraId == cameraId && f.SnapshotId == null)
            .OrderBy(f => f.UploadedAt).ThenBy(f => f.Id)
            .Take(max)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Frame>> ListBySnapshotAsync(Guid snapshotId, CancellationToken ct) =>
        await db.Frames.Where(f => f.SnapshotId == snapshotId).ToListAsync(ct);

    public async Task<IReadOnlyList<Frame>> ListByCameraAsync(Guid cameraId, int skip, int take, CancellationToken ct) =>
        await db.Frames.AsNoTracking()
            .Where(f => f.CameraId == cameraId)
            .OrderByDescending(f => f.UploadedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

    public void Add(Frame frame) => db.Frames.Add(frame);
}
