namespace Quorum.Application.Contracts;

/// <summary>Планировщик Datasets: у камеры накопилось достаточно новых кадров и подошёл срок цикла.</summary>
public sealed record AdaptationRequested(Guid CameraId, int NewFrames);

// Шаг 1 саги: снимок датасета
public sealed record CreateSnapshot(Guid CycleId, Guid CameraId);
public sealed record SnapshotCreated(Guid CycleId, Guid SnapshotId, Guid CameraId, string Task, string Prompt, int TrainFrames, int HoldoutFrames);
public sealed record SnapshotRejected(Guid CycleId, Guid CameraId, string Reason);

/// <summary>Завершение или компенсация: Discard = true возвращает кадры в «новые» для следующего цикла.</summary>
public sealed record ReleaseSnapshot(Guid CycleId, Guid SnapshotId, bool Discard);
public sealed record SnapshotReleased(Guid CycleId, Guid SnapshotId, bool Discarded);
