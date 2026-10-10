using Datasets.Application.Abstractions;
using Datasets.Domain.Snapshots;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;

namespace Datasets.Application.Features.Snapshots.ReleaseSnapshot;

public sealed class ReleaseSnapshotHandler(
    IFrameRepository frames,
    ISnapshotRepository snapshots,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<ReleaseSnapshotCommand>
{
    public async Task Handle(ReleaseSnapshotCommand cmd, CancellationToken ct)
    {
        var snapshot = await snapshots.GetAsync(cmd.SnapshotId, ct);
        if (snapshot is null) return;

        if (snapshot.Status == SnapshotStatus.Locked)
        {
            if (cmd.Discard)
            {
                snapshot.Discard();
                foreach (var frame in await frames.ListBySnapshotAsync(snapshot.Id, ct)) frame.Detach(snapshot.Id);
            }
            else
            {
                snapshot.MarkUsed();
            }
        }

        // Повторная команда (at-least-once) просто повторяет ответ.
        await events.PublishAsync(new SnapshotReleased(cmd.CycleId, snapshot.Id, snapshot.Status == SnapshotStatus.Discarded), ct);
        await uow.CommitAsync(ct);
    }
}
