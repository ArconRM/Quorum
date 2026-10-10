using Datasets.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Quorum.Application.Exceptions;

namespace Datasets.Infrastructure.Storage;

/// <summary>
/// Кадры на локальном диске / docker-томе (Storage:FramesRoot). В БД и в сообщениях хранится относительный путь
/// «{cameraId}/{frameId}.jpg» — воркер монтирует тот же том и читает файл по нему.
/// </summary>
internal sealed class LocalFrameStorage(IConfiguration config) : IFrameStorage
{
    private string Root => config["Storage:FramesRoot"] ?? "/data/frames";

    public async Task<string> SaveAsync(Guid cameraId, Guid frameId, string extension, Stream content, CancellationToken ct)
    {
        var relative = $"{cameraId:N}/{frameId:N}{extension}";
        var full = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var file = File.Create(full);
        await content.CopyToAsync(file, ct);
        return relative;
    }

    public Stream OpenRead(string storagePath)
    {
        var full = Path.GetFullPath(Path.Combine(Root, storagePath));
        if (!full.StartsWith(Path.GetFullPath(Root), StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid storage path");
        if (!File.Exists(full)) throw new NotFoundException($"File '{storagePath}' is missing in storage");
        return File.OpenRead(full);
    }
}
