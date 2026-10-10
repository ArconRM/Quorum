using Training.Domain.Evaluations;

namespace Training.Application.Abstractions;

public interface IEvaluationRepository
{
    Task<Evaluation?> GetByCycleAsync(Guid cycleId, CancellationToken ct);
    void Add(Evaluation evaluation);
}
