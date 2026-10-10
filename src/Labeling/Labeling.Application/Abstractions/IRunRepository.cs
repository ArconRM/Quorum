using Labeling.Domain.Runs;

namespace Labeling.Application.Abstractions;

public interface IRunRepository
{
    Task<LabelingRun?> GetAsync(Guid id, CancellationToken ct);
    Task<LabelingRun?> GetByCycleAsync(Guid cycleId, CancellationToken ct);
    void Add(LabelingRun run);
}
