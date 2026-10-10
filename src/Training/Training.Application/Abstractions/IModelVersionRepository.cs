using Training.Domain.Models;

namespace Training.Application.Abstractions;

public interface IModelVersionRepository
{
    Task<ModelVersion?> GetAsync(Guid id, CancellationToken ct);
    Task<ModelVersion?> GetPublishedAsync(Guid cameraId, CancellationToken ct);
    Task<IReadOnlyList<ModelVersion>> ListByCameraAsync(Guid cameraId, CancellationToken ct);
    Task<int> NextNumberAsync(Guid cameraId, CancellationToken ct);
    void Add(ModelVersion version);
}
