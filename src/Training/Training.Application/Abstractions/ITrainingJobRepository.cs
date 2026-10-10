using Training.Domain.Jobs;

namespace Training.Application.Abstractions;

public interface ITrainingJobRepository
{
    Task<TrainingJob?> GetAsync(Guid id, CancellationToken ct);
    void Add(TrainingJob job);
}
