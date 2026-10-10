using Quorum.Domain;
using Training.Domain.Common;
using Training.Domain.Jobs;

namespace Training.Domain.Models;

/// <summary>Корень агрегата: версия модели-студента для камеры.</summary>
public sealed class ModelVersion : Entity
{
    private ModelVersion() { }

    public Guid CameraId { get; private set; }
    public Guid CycleId { get; private set; }
    public Guid TrainingJobId { get; private set; }
    public int Number { get; private set; }
    public TaskType Task { get; private set; }
    public string Architecture { get; private set; } = string.Empty;
    public string ArtifactPath { get; private set; } = string.Empty;
    public string MetricName { get; private set; } = string.Empty;

    /// <summary>Метрика на отложенной выборке своего цикла (null — оценки ещё не было).</summary>
    public double? HoldoutMetric { get; private set; }

    public ModelVersionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public uint Version { get; private set; }

    public static ModelVersion CreateCandidate(TrainingJob job, int number)
    {
        if (job.Status != TrainingJobStatus.Succeeded) throw new DomainException("Only a succeeded job produces a model");
        if (number < 1) throw new DomainException("Version number must be positive");
        return new ModelVersion
        {
            Id = Guid.NewGuid(),
            CameraId = job.CameraId,
            CycleId = job.CycleId,
            TrainingJobId = job.Id,
            Number = number,
            Task = job.Task,
            Architecture = job.Architecture,
            ArtifactPath = job.ArtifactPath!,
            MetricName = job.MetricName ?? string.Empty,
            Status = ModelVersionStatus.Candidate,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void RecordEvaluation(double holdoutMetric)
    {
        if (Status != ModelVersionStatus.Candidate) throw new DomainException($"Version is {Status}");
        HoldoutMetric = holdoutMetric;
    }

    /// <summary>Кандидат после оценки или архивная версия при откате.</summary>
    internal void Publish()
    {
        if (Status is not (ModelVersionStatus.Candidate or ModelVersionStatus.Archived))
            throw new DomainException($"Cannot publish version in status {Status}");
        Status = ModelVersionStatus.Published;
        PublishedAt = DateTime.UtcNow;
    }

    internal void Archive()
    {
        if (Status != ModelVersionStatus.Published) throw new DomainException($"Cannot archive version in status {Status}");
        Status = ModelVersionStatus.Archived;
    }

    public void Reject()
    {
        if (Status != ModelVersionStatus.Candidate) throw new DomainException($"Cannot reject version in status {Status}");
        Status = ModelVersionStatus.Rejected;
    }
}
