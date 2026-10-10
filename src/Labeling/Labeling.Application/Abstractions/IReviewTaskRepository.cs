using Labeling.Domain.Reviews;

namespace Labeling.Application.Abstractions;

public interface IReviewTaskRepository
{
    Task<ReviewTask?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Незавершённое задание, уже взятое этим разметчиком.</summary>
    Task<ReviewTask?> FindClaimedByAsync(Guid annotatorId, CancellationToken ct);

    /// <summary>Самое старое открытое задание или задание, брошенное дольше таймаута.</summary>
    Task<ReviewTask?> NextAvailableAsync(DateTime now, CancellationToken ct);

    Task<IReadOnlyList<ReviewTask>> ListUnfinishedByRunAsync(Guid runId, CancellationToken ct);
    void Add(ReviewTask task);
}
