using Datasets.Application.Features.Snapshots.ReleaseSnapshot;
using MassTransit;
using MediatR;
using Quorum.Application.Contracts;

namespace Datasets.Infrastructure.Messaging;

internal sealed class ReleaseSnapshotConsumer(ISender sender) : IConsumer<ReleaseSnapshot>
{
    public Task Consume(ConsumeContext<ReleaseSnapshot> context) =>
        sender.Send(new ReleaseSnapshotCommand(context.Message.CycleId, context.Message.SnapshotId, context.Message.Discard),
            context.CancellationToken);
}
