using Training.Domain.Models;

namespace Training.Application.Models;

public sealed record ModelVersionDto(Guid Id, Guid CameraId, int Number, string Task, string Architecture, string ArtifactPath,
    string MetricName, double? HoldoutMetric, string Status, DateTime CreatedAt, DateTime? PublishedAt)
{
    public static ModelVersionDto From(ModelVersion v) => new(
        v.Id, v.CameraId, v.Number, v.Task.ToString(), v.Architecture, v.ArtifactPath, v.MetricName, v.HoldoutMetric,
        v.Status.ToString(), v.CreatedAt, v.PublishedAt);
}
