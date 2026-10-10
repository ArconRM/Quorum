using MediatR;

namespace Datasets.Application.Features.Snapshots.CreateSnapshot;

/// <summary>Шаг 1 саги: забрать новые кадры камеры в замороженный снимок.</summary>
public sealed record CreateSnapshotCommand(Guid CycleId, Guid CameraId) : IRequest;
