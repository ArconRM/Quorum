using Microsoft.EntityFrameworkCore;
using Training.Application.Abstractions;
using Training.Domain.Cycles;
using Training.Infrastructure.Db;

namespace Training.Infrastructure.Repositories;

internal sealed class CycleRepository(TrainingDbContext db) : ICycleRepository
{
    private static readonly CycleStatus[] Active =
        [CycleStatus.Snapshotting, CycleStatus.Labeling, CycleStatus.Training, CycleStatus.Evaluating];

    public Task<AdaptationCycle?> GetAsync(Guid id, CancellationToken ct) => db.Cycles.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<AdaptationCycle?> GetActiveForCameraAsync(Guid cameraId, CancellationToken ct) =>
        db.Cycles.FirstOrDefaultAsync(c => c.CameraId == cameraId && Active.Contains(c.Status), ct);

    public async Task<IReadOnlyList<AdaptationCycle>> ListAsync(Guid? cameraId, int take, CancellationToken ct) =>
        await db.Cycles.AsNoTracking()
            .Where(c => cameraId == null || c.CameraId == cameraId)
            .OrderByDescending(c => c.StartedAt)
            .Take(take)
            .ToListAsync(ct);

    public void Add(AdaptationCycle cycle) => db.Cycles.Add(cycle);
}
