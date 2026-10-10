using MediatR;

namespace Training.Application.Features.Cycles.OnSnapshotCreated;

public sealed record OnSnapshotCreatedCommand(Guid CycleId, Guid SnapshotId, string Task, string Prompt, int TrainFrames, int HoldoutFrames) : IRequest;
