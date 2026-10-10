using Training.Application.Models;

namespace Training.Application.Abstractions;

/// <summary>
/// Опубликованная модель камеры — её часто спрашивают клиенты на устройствах, поэтому чтение идёт через Redis.
/// Инвалидация — после коммита публикации или отката.
/// </summary>
public interface IPublishedModelCache
{
    Task<ModelVersionDto?> GetAsync(Guid cameraId, CancellationToken ct);
    Task InvalidateAsync(Guid cameraId, CancellationToken ct);
}
