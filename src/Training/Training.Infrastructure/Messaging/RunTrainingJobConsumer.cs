using MassTransit;
using MediatR;
using Training.Application.Features.Jobs.RunTrainingJob;
using Training.Application.Messages;

namespace Training.Infrastructure.Messaging;

internal sealed class RunTrainingJobConsumer(ISender sender) : IConsumer<RunTrainingJob>
{
    public Task Consume(ConsumeContext<RunTrainingJob> context) =>
        sender.Send(new RunTrainingJobCommand(context.Message.JobId), context.CancellationToken);
}

internal sealed class RunTrainingJobConsumerDefinition : ConsumerDefinition<RunTrainingJobConsumer>
{
    public RunTrainingJobConsumerDefinition() => ConcurrentMessageLimit = 1; // одна GPU — одно обучение за раз
}
