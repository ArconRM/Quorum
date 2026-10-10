using Labeling.Application.Features.Runs.DiscardLabelingRun;
using MassTransit;
using MediatR;
using Quorum.Application.Contracts;

namespace Labeling.Infrastructure.Messaging;

internal sealed class DiscardLabelingConsumer(ISender sender) : IConsumer<DiscardLabeling>
{
    public Task Consume(ConsumeContext<DiscardLabeling> context)
    {
        var m = context.Message;
        return sender.Send(new DiscardLabelingRunCommand(m.CycleId), context.CancellationToken);
    }
}
