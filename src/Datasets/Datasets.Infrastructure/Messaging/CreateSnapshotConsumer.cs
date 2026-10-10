using Datasets.Application.Features.Snapshots.CreateSnapshot;
using MassTransit;
using MediatR;
using Quorum.Application.Contracts;

namespace Datasets.Infrastructure.Messaging;

internal sealed class CreateSnapshotConsumer(ISender sender) : IConsumer<CreateSnapshot>
{
    public Task Consume(ConsumeContext<CreateSnapshot> context) =>
        sender.Send(new CreateSnapshotCommand(context.Message.CycleId, context.Message.CameraId), context.CancellationToken);
}
