using Microsoft.Extensions.Caching.Hybrid;
using Training.Application.Abstractions;
using Training.Application.Models;

namespace Training.Infrastructure.Caching;

/// <summary>HybridCache: L1 — память процесса, L2 — Redis. Один запрос в БД на ключ даже при наплыве (stampede protection).</summary>
internal sealed class PublishedModelCache(IModelVersionRepository versions, HybridCache cache) : IPublishedModelCache
{
    private static readonly HybridCacheEntryOptions Options = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromSeconds(30),
    };

    private static string Key(Guid cameraId) => $"published:{cameraId}";

    public async Task<ModelVersionDto?> GetAsync(Guid cameraId, CancellationToken ct) =>
        await cache.GetOrCreateAsync<ModelVersionDto?>(
            Key(cameraId),
            async token => await versions.GetPublishedAsync(cameraId, token) is { } v ? ModelVersionDto.From(v) : null,
            Options,
            cancellationToken: ct);

    public Task InvalidateAsync(Guid cameraId, CancellationToken ct) => cache.RemoveAsync(Key(cameraId), ct).AsTask();
}
