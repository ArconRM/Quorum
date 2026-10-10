using MediatR;
using Quorum.Application.Abstractions;
using Training.Application.Abstractions;
using Training.Domain.Cycles;

namespace Training.Application.Features.Cycles.OnSnapshotRejected;

public sealed class OnSnapshotRejectedHandler(ICycleRepository cycles, IUnitOfWork uow) : IRequestHandler<OnSnapshotRejectedCommand>
{
    public async Task Handle(OnSnapshotRejectedCommand cmd, CancellationToken ct)
    {
        var cycle = await cycles.GetAsync(cmd.CycleId, ct);
        if (cycle is not { Status: CycleStatus.Snapshotting }) return;
        cycle.Skip(cmd.Reason);
        await uow.CommitAsync(ct);
    }
}
