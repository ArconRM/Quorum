using MassTransit;
using MediatR;
using Quorum.Application.Contracts;
using Training.Application.Features.Cycles.OnLabelingFailed;

namespace Training.Infrastructure.Messaging;

internal sealed class LabelingFailedConsumer(ISender sender) : IConsumer<LabelingFailed>
{
    public Task Consume(ConsumeContext<LabelingFailed> context)
    {
        var m = context.Message;
        return sender.Send(new OnLabelingFailedCommand(m.CycleId, m.Reason), context.CancellationToken);
    }
}
