namespace Quorum.Application.Contracts;

/// <summary>На камере опубликована новая версия модели (в т.ч. после отката).</summary>
public sealed record ModelPublished(Guid CameraId, Guid VersionId, int Number, string MetricName, double MetricValue);
