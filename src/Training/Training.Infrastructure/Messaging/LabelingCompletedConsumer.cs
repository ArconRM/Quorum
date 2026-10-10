using MassTransit;
using MediatR;
using Quorum.Application.Contracts;
using Training.Application.Features.Cycles.OnLabelingCompleted;

namespace Training.Infrastructure.Messaging;

internal sealed class LabelingCompletedConsumer(ISender sender) : IConsumer<LabelingCompleted>
{
    public Task Consume(ConsumeContext<LabelingCompleted> context)
    {
        var m = context.Message;
        return sender.Send(new OnLabelingCompletedCommand(m.CycleId, m.RunId, m.Accepted), context.CancellationToken);
    }
}
