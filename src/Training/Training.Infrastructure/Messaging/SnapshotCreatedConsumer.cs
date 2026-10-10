using MassTransit;
using MediatR;
using Quorum.Application.Contracts;
using Training.Application.Features.Cycles.OnSnapshotCreated;

namespace Training.Infrastructure.Messaging;

internal sealed class SnapshotCreatedConsumer(ISender sender) : IConsumer<SnapshotCreated>
{
    public Task Consume(ConsumeContext<SnapshotCreated> context)
    {
        var m = context.Message;
        return sender.Send(new OnSnapshotCreatedCommand(m.CycleId, m.SnapshotId, m.Task, m.Prompt, m.TrainFrames, m.HoldoutFrames), context.CancellationToken);
    }
}
