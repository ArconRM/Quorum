using Quorum.Domain;

namespace Datasets.Domain.Frames;

/// <summary>
/// Корень агрегата: кадр с камеры. Сам файл лежит в хранилище, здесь — метаданные.
/// SnapshotId != null — кадр забран в снимок и в следующий цикл не попадёт (если снимок не отброшен).
/// </summary>
public sealed class Frame : Entity
{
    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string> { "image/jpeg", "image/png", "image/webp", "image/bmp" };

    private Frame() { }

    public Guid CameraId { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public DateTime CapturedAt { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public Guid? SnapshotId { get; private set; }

    /// <summary>Токен оптимистичной конкурентности (xmin): два снимка не заберут один кадр.</summary>
    public uint Version { get; private set; }

    public static Frame Create(Guid id, Guid cameraId, string storagePath, string contentType, long sizeBytes, DateTime? capturedAt)
    {
        if (id == Guid.Empty || cameraId == Guid.Empty) throw new DomainException("Frame and camera ids are required");
        if (string.IsNullOrWhiteSpace(storagePath)) throw new DomainException("Storage path is required");
        if (!AllowedContentTypes.Contains(contentType)) throw new DomainException($"Unsupported content type '{contentType}'");
        if (sizeBytes <= 0) throw new DomainException("Frame file is empty");

        var now = DateTime.UtcNow;
        var captured = capturedAt?.ToUniversalTime() ?? now;
        if (captured > now.AddMinutes(5)) throw new DomainException("Capture time is in the future");

        return new Frame
        {
            Id = id,
            CameraId = cameraId,
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            CapturedAt = captured,
            UploadedAt = now,
        };
    }

    public void AttachTo(Guid snapshotId)
    {
        if (SnapshotId is not null && SnapshotId != snapshotId)
            throw new DomainException($"Frame {Id} already belongs to snapshot {SnapshotId}");
        SnapshotId = snapshotId;
    }

    /// <summary>Компенсация: снимок отброшен — кадр снова «новый».</summary>
    public void Detach(Guid snapshotId)
    {
        if (SnapshotId == snapshotId) SnapshotId = null;
    }
}
