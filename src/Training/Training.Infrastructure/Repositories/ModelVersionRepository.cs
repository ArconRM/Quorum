using Microsoft.EntityFrameworkCore;
using Training.Application.Abstractions;
using Training.Domain.Models;
using Training.Infrastructure.Db;

namespace Training.Infrastructure.Repositories;

internal sealed class ModelVersionRepository(TrainingDbContext db) : IModelVersionRepository
{
    public Task<ModelVersion?> GetAsync(Guid id, CancellationToken ct) => db.Versions.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<ModelVersion?> GetPublishedAsync(Guid cameraId, CancellationToken ct) =>
        db.Versions.FirstOrDefaultAsync(v => v.CameraId == cameraId && v.Status == ModelVersionStatus.Published, ct);

    public async Task<IReadOnlyList<ModelVersion>> ListByCameraAsync(Guid cameraId, CancellationToken ct) =>
        await db.Versions.AsNoTracking().Where(v => v.CameraId == cameraId).OrderByDescending(v => v.Number).ToListAsync(ct);

    public async Task<int> NextNumberAsync(Guid cameraId, CancellationToken ct) =>
        (await db.Versions.Where(v => v.CameraId == cameraId).MaxAsync(v => (int?)v.Number, ct) ?? 0) + 1;

    public void Add(ModelVersion version) => db.Versions.Add(version);
}
