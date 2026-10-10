using Datasets.Domain.Cameras;

namespace Datasets.Application.Abstractions;

public interface ICameraRepository
{
    Task<Camera?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Camera>> ListAsync(bool onlyActive, CancellationToken ct);
    void Add(Camera camera);
}
