using Labeling.Application.Abstractions;
using Labeling.Domain.Runs;
using Labeling.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace Labeling.Infrastructure.Repositories;

internal sealed class RunRepository(LabelingDbContext db) : IRunRepository
{
    public Task<LabelingRun?> GetAsync(Guid id, CancellationToken ct) => db.Runs.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<LabelingRun?> GetByCycleAsync(Guid cycleId, CancellationToken ct) =>
        db.Runs.FirstOrDefaultAsync(r => r.CycleId == cycleId, ct);

    public void Add(LabelingRun run) => db.Runs.Add(run);
}
