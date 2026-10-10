using MassTransit;
using MediatR;
using Quorum.Application.Contracts;
using Training.Application.Features.Cycles.OnSnapshotRejected;

namespace Training.Infrastructure.Messaging;

internal sealed class SnapshotRejectedConsumer(ISender sender) : IConsumer<SnapshotRejected>
{
    public Task Consume(ConsumeContext<SnapshotRejected> context)
    {
        var m = context.Message;
        return sender.Send(new OnSnapshotRejectedCommand(m.CycleId, m.Reason), context.CancellationToken);
    }
}
