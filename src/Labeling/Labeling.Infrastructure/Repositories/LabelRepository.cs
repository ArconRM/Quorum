using Labeling.Application.Abstractions;
using Labeling.Domain.Labels;
using Labeling.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace Labeling.Infrastructure.Repositories;

internal sealed class LabelRepository(LabelingDbContext db) : ILabelRepository
{
    public Task<ConsensusLabel?> GetAsync(Guid id, CancellationToken ct) => db.Labels.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<ConsensusLabel>> ListByRunAsync(Guid runId, LabelStatus? status, int skip, int take, CancellationToken ct) =>
        await db.Labels.AsNoTracking()
            .Where(l => l.RunId == runId && (status == null || l.Status == status))
            .OrderBy(l => l.Agreement).ThenBy(l => l.FrameId) // самые спорные — первыми
            .Skip(skip).Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ConsensusLabel>> ListAcceptedAsync(Guid runId, CancellationToken ct) =>
        await db.Labels.AsNoTracking().Where(l => l.RunId == runId && l.Status == LabelStatus.Accepted).ToListAsync(ct);

    public void Add(ConsensusLabel label) => db.Labels.Add(label);
}
