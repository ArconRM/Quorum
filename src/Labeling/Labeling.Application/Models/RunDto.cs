using Labeling.Domain.Runs;

namespace Labeling.Application.Models;

public sealed record RunTeacherDto(Guid TeacherId, string Name, double Weight, int ProcessedFrames, bool IsCompleted);

public sealed record RunDto(
    Guid Id, Guid CycleId, Guid SnapshotId, string Task, string ValidationMode, double AgreementThreshold, string Status,
    int TotalFrames, int Accepted, int Rejected, int PendingReview, int Reviewed, string? FailureReason,
    DateTime CreatedAt, DateTime? FinishedAt, IReadOnlyList<RunTeacherDto> Teachers)
{
    public static RunDto From(LabelingRun r) => new(
        r.Id, r.CycleId, r.SnapshotId, r.Task.ToString(), r.Strategy.Mode.ToString(), r.Strategy.AgreementThreshold,
        r.Status.ToString(), r.TotalFrames, r.Accepted, r.Rejected, r.PendingReview, r.Reviewed, r.FailureReason,
        r.CreatedAt, r.FinishedAt,
        r.Teachers.Select(t => new RunTeacherDto(t.TeacherId, t.Name, t.Weight, t.ProcessedFrames, t.IsCompleted)).ToList());
}
