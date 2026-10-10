using MassTransit;
using MediatR;
using Training.Application.Messages;
using Training.Application.Features.Cycles.EvaluateCandidate;

namespace Training.Infrastructure.Messaging;

internal sealed class EvaluateCandidateConsumer(ISender sender) : IConsumer<EvaluateCandidate>
{
    public Task Consume(ConsumeContext<EvaluateCandidate> context)
    {
        var m = context.Message;
        return sender.Send(new EvaluateCandidateCommand(m.CycleId), context.CancellationToken);
    }
}
