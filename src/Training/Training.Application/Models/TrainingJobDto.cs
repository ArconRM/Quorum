using Training.Domain.Jobs;

namespace Training.Application.Models;

public sealed record TrainingJobDto(Guid Id, Guid CycleId, Guid CameraId, string Task, string Architecture, int Epochs, int Epoch,
    double? Loss, int TrainSamples, string Status, string? ArtifactPath, string? MetricName, double? MetricValue,
    Guid? BaseVersionId, string? FailureReason, DateTime CreatedAt, DateTime? FinishedAt)
{
    public static TrainingJobDto From(TrainingJob j) => new(
        j.Id, j.CycleId, j.CameraId, j.Task.ToString(), j.Architecture, j.Epochs, j.Epoch, j.Loss, j.TrainSamples,
        j.Status.ToString(), j.ArtifactPath, j.MetricName, j.MetricValue, j.BaseVersionId, j.FailureReason, j.CreatedAt, j.FinishedAt);
}
