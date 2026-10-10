using MassTransit;
using MediatR;
using Quorum.Application.Contracts;
using Training.Application.Features.Cycles.StartCycle;

namespace Training.Infrastructure.Messaging;

/// <summary>Планировщик Datasets попросил цикл (хореография) — Training становится оркестратором.</summary>
internal sealed class AdaptationRequestedConsumer(ISender sender) : IConsumer<AdaptationRequested>
{
    public Task Consume(ConsumeContext<AdaptationRequested> context)
    {
        var m = context.Message;
        return sender.Send(new StartCycleCommand(m.CameraId, Scheduled: true, null, null), context.CancellationToken);
    }
}
