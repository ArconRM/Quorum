using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;
using Quorum.Domain;
using Training.Application.Abstractions;
using Training.Domain.Cycles;

namespace Training.Application.Features.Cycles.StartCycle;

public sealed class StartCycleHandler(
    ICycleRepository cycles,
    IAdaptationSettings settings,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<StartCycleCommand, Guid>
{
    public async Task<Guid> Handle(StartCycleCommand cmd, CancellationToken ct)
    {
        // На камере одновременно идёт не больше одного цикла.
        var active = await cycles.GetActiveForCameraAsync(cmd.CameraId, ct);
        if (active is not null)
        {
            if (cmd.Scheduled) return active.Id; // повторный тик планировщика — не ошибка
            throw new DomainException($"Cycle {active.Id} is already running for this camera");
        }

        var mode = Enum.Parse<ValidationMode>(cmd.ValidationMode ?? settings.DefaultValidationMode, ignoreCase: true);
        var cycle = AdaptationCycle.Start(cmd.CameraId, cmd.Scheduled ? CycleTrigger.Scheduled : CycleTrigger.Manual,
            ValidationSettings.Create(mode, cmd.AgreementThreshold ?? settings.DefaultAgreementThreshold));
        cycles.Add(cycle);

        await events.PublishAsync(new CreateSnapshot(cycle.Id, cycle.CameraId), ct);
        await uow.CommitAsync(ct);
        return cycle.Id;
    }
}
