using MediatR;
using Quorum.Application.Abstractions;
using Training.Application.Abstractions;
using Training.Application.Sagas;
using Training.Domain.Cycles;

namespace Training.Application.Features.Cycles.OnLabelingFailed;

/// <summary>Компенсация шага 1: снимок отбрасывается, кадры вернутся в следующий цикл.</summary>
public sealed class OnLabelingFailedHandler(ICycleRepository cycles, IIntegrationEventPublisher events, IUnitOfWork uow)
    : IRequestHandler<OnLabelingFailedCommand>
{
    public async Task Handle(OnLabelingFailedCommand cmd, CancellationToken ct)
    {
        var cycle = await cycles.GetAsync(cmd.CycleId, ct);
        if (cycle is not { Status: CycleStatus.Labeling }) return;

        var compensation = cycle.OnLabelingFailed(cmd.Reason);
        await Compensations.PublishAsync(events, cycle, compensation, ct);
        await uow.CommitAsync(ct);
    }
}
