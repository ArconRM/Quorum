using Training.Domain.Jobs;

namespace Training.Application.Abstractions;

public sealed record TrainingResult(string ArtifactPath, string MetricName, double MetricValue);

public sealed record EvaluationScores(string MetricName, double Candidate, double? Baseline);

/// <summary>Порт к GPU-воркеру. Бросает ExternalServiceException, если воркер недоступен или упал.</summary>
public interface ITrainingWorker
{
    /// <param name="onEpoch">Вызывается после каждой эпохи: номер эпохи и loss.</param>
    Task<TrainingResult> TrainAsync(TrainingJob job, IReadOnlyList<LabeledSample> trainSet,
        Func<int, double, Task> onEpoch, CancellationToken ct);

    Task<EvaluationScores> EvaluateAsync(string task, string candidateArtifact, string? baselineArtifact,
        IReadOnlyList<LabeledSample> holdout, CancellationToken ct);
}
