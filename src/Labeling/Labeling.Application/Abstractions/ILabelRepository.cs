using Labeling.Domain.Labels;

namespace Labeling.Application.Abstractions;

public interface ILabelRepository
{
    Task<ConsensusLabel?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ConsensusLabel>> ListByRunAsync(Guid runId, LabelStatus? status, int skip, int take, CancellationToken ct);
    Task<IReadOnlyList<ConsensusLabel>> ListAcceptedAsync(Guid runId, CancellationToken ct);
    void Add(ConsensusLabel label);
}
