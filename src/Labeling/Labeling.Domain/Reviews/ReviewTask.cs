using Labeling.Domain.Labels;
using Quorum.Domain;

namespace Labeling.Domain.Reviews;

/// <summary>
/// Корень агрегата: задание разметчику проверить одну спорную метку.
/// Open → Claimed (взял разметчик) → Done; брошенное больше чем на ClaimTimeout задание может взять другой.
/// </summary>
public sealed class ReviewTask : Entity
{
    public static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(30);

    private ReviewTask() { }

    public Guid RunId { get; private set; }
    public Guid LabelId { get; private set; }
    public Guid FrameId { get; private set; }
    public ReviewTaskStatus Status { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ClaimedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public ReviewVerdict? Verdict { get; private set; }
    public uint Version { get; private set; }

    public static ReviewTask For(ConsensusLabel label)
    {
        if (label.Status != LabelStatus.NeedsReview) throw new DomainException("Only disputed labels need review");
        return new ReviewTask
        {
            Id = Guid.NewGuid(),
            RunId = label.RunId,
            LabelId = label.Id,
            FrameId = label.FrameId,
            Status = ReviewTaskStatus.Open,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public bool IsClaimStale(DateTime now) =>
        Status == ReviewTaskStatus.Claimed && ClaimedAt is { } at && now - at > ClaimTimeout;

    public void Claim(Guid annotatorId, DateTime now)
    {
        if (Status == ReviewTaskStatus.Claimed && AssigneeId == annotatorId) return;
        if (Status != ReviewTaskStatus.Open && !IsClaimStale(now))
            throw new DomainException($"Review task is {Status}");
        Status = ReviewTaskStatus.Claimed;
        AssigneeId = annotatorId;
        ClaimedAt = now;
    }

    public void Complete(Guid annotatorId, ReviewVerdict verdict)
    {
        if (Status != ReviewTaskStatus.Claimed) throw new DomainException($"Review task is {Status}");
        if (AssigneeId != annotatorId) throw new DomainException("Review task is assigned to another annotator");
        Status = ReviewTaskStatus.Done;
        Verdict = verdict;
        CompletedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status is ReviewTaskStatus.Open or ReviewTaskStatus.Claimed) Status = ReviewTaskStatus.Cancelled;
    }
}
