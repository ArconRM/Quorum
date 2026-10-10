using Datasets.Domain.Cameras;
using Quorum.Domain;

namespace Datasets.Domain.Snapshots;

/// <summary>
/// Корень агрегата: замороженный набор кадров под один цикл адаптации.
/// Пока снимок Locked, его состав не меняется; на камере одновременно может быть только один Locked-снимок.
/// </summary>
public sealed class DatasetSnapshot : Entity
{
    /// <summary>Каждый N-й кадр уходит в отложенную выборку (20%).</summary>
    public const int HoldoutEvery = 5;

    private List<SnapshotFrame> _frames = [];

    private DatasetSnapshot() { }

    public Guid CameraId { get; private set; }
    public Guid CycleId { get; private set; }
    public TaskType Task { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public SnapshotStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public IReadOnlyList<SnapshotFrame> Frames => _frames;

    public int TrainCount => _frames.Count(f => f.Split == DataSplit.Train);
    public int HoldoutCount => _frames.Count(f => f.Split == DataSplit.Holdout);

    /// <param name="frames">Кадры в порядке поступления (id и путь к файлу).</param>
    public static DatasetSnapshot Create(Camera camera, Guid cycleId, IReadOnlyList<(Guid FrameId, string StoragePath)> frames)
    {
        if (cycleId == Guid.Empty) throw new DomainException("Cycle is required");
        if (frames.Count < camera.MinFramesPerCycle)
            throw new DomainException($"Not enough new frames: {frames.Count} < {camera.MinFramesPerCycle}");
        if (frames.Select(f => f.FrameId).Distinct().Count() != frames.Count)
            throw new DomainException("Snapshot frames must be unique");

        // Детерминированное разбиение: воспроизводимо и не зависит от генератора случайных чисел.
        // Если кадров меньше HoldoutEvery, все идут в обучение.
        var list = frames
            .Select((f, i) => new SnapshotFrame(f.FrameId, f.StoragePath,
                (i + 1) % HoldoutEvery == 0 ? DataSplit.Holdout : DataSplit.Train))
            .ToList();

        return new DatasetSnapshot
        {
            Id = Guid.NewGuid(),
            CameraId = camera.Id,
            CycleId = cycleId,
            Task = camera.Task,
            Prompt = camera.Prompt,
            Status = SnapshotStatus.Locked,
            CreatedAt = DateTime.UtcNow,
            _frames = list,
        };
    }

    public void MarkUsed()
    {
        EnsureLocked();
        Status = SnapshotStatus.Used;
        ReleasedAt = DateTime.UtcNow;
    }

    public void Discard()
    {
        EnsureLocked();
        Status = SnapshotStatus.Discarded;
        ReleasedAt = DateTime.UtcNow;
    }

    private void EnsureLocked()
    {
        if (Status != SnapshotStatus.Locked) throw new DomainException($"Snapshot is already {Status}");
    }
}
