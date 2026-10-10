using MediatR;

namespace Datasets.Application.Features.Snapshots.ReleaseSnapshot;

/// <summary>
/// Завершение цикла (Discard = false: кадры использованы) или компенсация (Discard = true: кадры
/// возвращаются в «новые» и попадут в следующий цикл).
/// </summary>
public sealed record ReleaseSnapshotCommand(Guid CycleId, Guid SnapshotId, bool Discard) : IRequest;
