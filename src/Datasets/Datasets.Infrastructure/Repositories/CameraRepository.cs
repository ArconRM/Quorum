using Datasets.Application.Abstractions;
using Datasets.Domain.Cameras;
using Datasets.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace Datasets.Infrastructure.Repositories;

internal sealed class CameraRepository(DatasetsDbContext db) : ICameraRepository
{
    public Task<Camera?> GetAsync(Guid id, CancellationToken ct) => db.Cameras.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Camera>> ListAsync(bool onlyActive, CancellationToken ct) =>
        await db.Cameras.Where(c => !onlyActive || c.IsActive).OrderBy(c => c.Name).ToListAsync(ct);

    public void Add(Camera camera) => db.Cameras.Add(camera);
}
