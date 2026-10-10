using Training.Domain.Cycles;

namespace Training.Application.Abstractions;

public interface ICycleRepository
{
    Task<AdaptationCycle?> GetAsync(Guid id, CancellationToken ct);
    Task<AdaptationCycle?> GetActiveForCameraAsync(Guid cameraId, CancellationToken ct);
    Task<IReadOnlyList<AdaptationCycle>> ListAsync(Guid? cameraId, int take, CancellationToken ct);
    void Add(AdaptationCycle cycle);
}
