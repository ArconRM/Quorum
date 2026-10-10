using Quorum.Domain;
using Training.Domain.Common;

namespace Training.Domain.Jobs;

/// <summary>Корень агрегата: обучение студента на псевдоразметке цикла.</summary>
public sealed class TrainingJob : Entity
{
    private TrainingJob() { }

    public Guid CycleId { get; private set; }
    public Guid CameraId { get; private set; }
    public Guid LabelingRunId { get; private set; }
    public TaskType Task { get; private set; }
    public string Architecture { get; private set; } = string.Empty;
    public int Epochs { get; private set; }

    /// <summary>Текущая опубликованная модель камеры — студент дообучается от неё, а не с нуля.</summary>
    public Guid? BaseVersionId { get; private set; }
    public string? BaseArtifact { get; private set; }

    public TrainingJobStatus Status { get; private set; }
    public int Epoch { get; private set; }
    public double? Loss { get; private set; }
    public int TrainSamples { get; private set; }
    public string? ArtifactPath { get; private set; }
    public string? MetricName { get; private set; }
    public double? MetricValue { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }

    /// <summary>Лёгкие модели реального времени — то, ради чего всё затевается.</summary>
    public static string ArchitectureFor(TaskType task) => task == TaskType.Detection ? "yolov8n" : "crnn";

    public static TrainingJob Create(Guid cycleId, Guid cameraId, Guid labelingRunId, TaskType task, int epochs,
        Guid? baseVersionId, string? baseArtifact)
    {
        if (epochs is < 1 or > 1000) throw new DomainException("Epochs must be in [1, 1000]");
        return new TrainingJob
        {
            Id = Guid.NewGuid(),
            CycleId = cycleId,
            CameraId = cameraId,
            LabelingRunId = labelingRunId,
            Task = task,
            Architecture = ArchitectureFor(task),
            Epochs = epochs,
            BaseVersionId = baseVersionId,
            BaseArtifact = baseArtifact,
            Status = TrainingJobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void Start(int trainSamples)
    {
        if (Status != TrainingJobStatus.Queued) throw new DomainException($"Job is {Status}");
        if (trainSamples < 1) throw new DomainException("No training samples");
        TrainSamples = trainSamples;
        Status = TrainingJobStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public void ReportProgress(int epoch, double loss)
    {
        if (Status != TrainingJobStatus.Running) throw new DomainException($"Job is {Status}");
        Epoch = Math.Clamp(epoch, 0, Epochs);
        Loss = loss;
    }

    public void Succeed(string artifactPath, string metricName, double metricValue)
    {
        if (Status != TrainingJobStatus.Running) throw new DomainException($"Job is {Status}");
        if (string.IsNullOrWhiteSpace(artifactPath)) throw new DomainException("Trained model artifact is required");
        ArtifactPath = artifactPath;
        MetricName = metricName;
        MetricValue = metricValue;
        Epoch = Epochs;
        Status = TrainingJobStatus.Succeeded;
        FinishedAt = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        if (Status is TrainingJobStatus.Succeeded or TrainingJobStatus.Failed) throw new DomainException($"Job is {Status}");
        FailureReason = reason;
        Status = TrainingJobStatus.Failed;
        FinishedAt = DateTime.UtcNow;
    }
}
