using Labeling.Application.Abstractions;
using Labeling.Domain.Reviews;
using Labeling.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace Labeling.Infrastructure.Repositories;

internal sealed class ReviewTaskRepository(LabelingDbContext db) : IReviewTaskRepository
{
    public Task<ReviewTask?> GetAsync(Guid id, CancellationToken ct) => db.Reviews.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<ReviewTask?> FindClaimedByAsync(Guid annotatorId, CancellationToken ct) =>
        db.Reviews.FirstOrDefaultAsync(t => t.AssigneeId == annotatorId && t.Status == ReviewTaskStatus.Claimed, ct);

    public Task<ReviewTask?> NextAvailableAsync(DateTime now, CancellationToken ct)
    {
        var staleBefore = now - ReviewTask.ClaimTimeout;
        return db.Reviews
            .Where(t => t.Status == ReviewTaskStatus.Open
                        || (t.Status == ReviewTaskStatus.Claimed && t.ClaimedAt < staleBefore))
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ReviewTask>> ListUnfinishedByRunAsync(Guid runId, CancellationToken ct) =>
        await db.Reviews
            .Where(t => t.RunId == runId && (t.Status == ReviewTaskStatus.Open || t.Status == ReviewTaskStatus.Claimed))
            .ToListAsync(ct);

    public void Add(ReviewTask task) => db.Reviews.Add(task);
}
