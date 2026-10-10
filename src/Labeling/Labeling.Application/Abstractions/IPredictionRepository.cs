using Labeling.Domain.Predictions;

namespace Labeling.Application.Abstractions;

public interface IPredictionRepository
{
    Task<IReadOnlyList<Prediction>> ListByRunAsync(Guid runId, CancellationToken ct);
    Task<IReadOnlyList<Prediction>> ListByFrameAsync(Guid runId, Guid frameId, CancellationToken ct);

    /// <summary>Кадры, по которым у учителя уже есть ответ (для дозапуска после сбоя).</summary>
    Task<IReadOnlySet<Guid>> FramesWithPredictionsAsync(Guid runId, Guid teacherId, CancellationToken ct);

    void Add(Prediction prediction);
}
