namespace Datasets.Application.Abstractions;

/// <summary>Хранилище файлов кадров (локальный том; позже можно заменить на S3/MinIO).</summary>
public interface IFrameStorage
{
    /// <returns>Относительный путь, по которому файл доступен остальным сервисам и воркеру.</returns>
    Task<string> SaveAsync(Guid cameraId, Guid frameId, string extension, Stream content, CancellationToken ct);

    Stream OpenRead(string storagePath);
}
