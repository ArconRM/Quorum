using MediatR;

namespace Training.Application.Features.Cycles.OnSnapshotRejected;

public sealed record OnSnapshotRejectedCommand(Guid CycleId, string Reason) : IRequest;
