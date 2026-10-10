using Labeling.Application.Features.Runs.ProcessLabelingRun;
using Labeling.Application.Messages;
using MassTransit;
using MediatR;

namespace Labeling.Infrastructure.Messaging;

/// <summary>Долгий шаг: прогон учителей через воркер и консенсус. Отдельная очередь — не блокирует приём команд саги.</summary>
internal sealed class ProcessLabelingRunConsumer(ISender sender) : IConsumer<ProcessLabelingRun>
{
    public Task Consume(ConsumeContext<ProcessLabelingRun> context) =>
        sender.Send(new ProcessLabelingRunCommand(context.Message.RunId), context.CancellationToken);
}

internal sealed class ProcessLabelingRunConsumerDefinition : ConsumerDefinition<ProcessLabelingRunConsumer>
{
    public ProcessLabelingRunConsumerDefinition() => ConcurrentMessageLimit = 1; // одна GPU — один прогон за раз
}
