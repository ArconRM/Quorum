using Labeling.Application.Features.Runs.StartLabelingRun;
using MassTransit;
using MediatR;
using Quorum.Application.Contracts;

namespace Labeling.Infrastructure.Messaging;

internal sealed class StartLabelingConsumer(ISender sender) : IConsumer<StartLabeling>
{
    public Task Consume(ConsumeContext<StartLabeling> context)
    {
        var m = context.Message;
        return sender.Send(new StartLabelingRunCommand(m.CycleId, m.SnapshotId, m.Task, m.Prompt, m.ValidationMode, m.AgreementThreshold), context.CancellationToken);
    }
}
