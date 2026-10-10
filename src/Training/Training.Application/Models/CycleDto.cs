using Training.Domain.Cycles;
using Training.Domain.Evaluations;

namespace Training.Application.Models;

public sealed record EvaluationDto(string MetricName, double CandidateScore, double? BaselineScore, int HoldoutSize, string Decision)
{
    public static EvaluationDto From(Evaluation e) =>
        new(e.MetricName, e.CandidateScore, e.BaselineScore, e.HoldoutSize, e.Decision.ToString());
}

public sealed record CycleDto(
    Guid Id, Guid CameraId, string Trigger, string ValidationMode, double AgreementThreshold, string Status, string? Outcome,
    string? Task, Guid? SnapshotId, int TrainFrames, int HoldoutFrames, Guid? LabelingRunId, int AcceptedLabels,
    Guid? TrainingJobId, Guid? CandidateVersionId, string? FailureReason, DateTime StartedAt, DateTime? FinishedAt,
    EvaluationDto? Evaluation)
{
    public static CycleDto From(AdaptationCycle c, Evaluation? evaluation = null) => new(
        c.Id, c.CameraId, c.Trigger.ToString(), c.Validation.Mode.ToString(), c.Validation.AgreementThreshold,
        c.Status.ToString(), c.Outcome?.ToString(), c.Task?.ToString(), c.SnapshotId, c.TrainFrames, c.HoldoutFrames,
        c.LabelingRunId, c.AcceptedLabels, c.TrainingJobId, c.CandidateVersionId, c.FailureReason, c.StartedAt, c.FinishedAt,
        evaluation is null ? null : EvaluationDto.From(evaluation));
}
