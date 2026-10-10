using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;
using Training.Application.Abstractions;
using Training.Domain.Common;
using Training.Domain.Cycles;

namespace Training.Application.Features.Cycles.OnSnapshotCreated;

/// <summary>Шаг 1 → 2: снимок готов, запускаем разметку.</summary>
public sealed class OnSnapshotCreatedHandler(ICycleRepository cycles, IIntegrationEventPublisher events, IUnitOfWork uow)
    : IRequestHandler<OnSnapshotCreatedCommand>
{
    public async Task Handle(OnSnapshotCreatedCommand cmd, CancellationToken ct)
    {
        var cycle = await cycles.GetAsync(cmd.CycleId, ct);
        if (cycle is not { Status: CycleStatus.Snapshotting }) return; // повтор или чужое событие

        cycle.OnSnapshotCreated(cmd.SnapshotId, Enum.Parse<TaskType>(cmd.Task, ignoreCase: true), cmd.TrainFrames, cmd.HoldoutFrames);
        await events.PublishAsync(new StartLabeling(cycle.Id, cmd.SnapshotId, cmd.Task, cmd.Prompt,
            cycle.Validation.Mode.ToString(), cycle.Validation.AgreementThreshold), ct);
        await uow.CommitAsync(ct);
    }
}
