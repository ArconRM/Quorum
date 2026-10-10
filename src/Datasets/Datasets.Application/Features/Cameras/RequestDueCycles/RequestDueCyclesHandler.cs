using Datasets.Application.Abstractions;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;

namespace Datasets.Application.Features.Cameras.RequestDueCycles;

public sealed class RequestDueCyclesHandler(
    ICameraRepository cameras,
    IFrameRepository frames,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<RequestDueCyclesCommand, int>
{
    public async Task<int> Handle(RequestDueCyclesCommand cmd, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var requested = 0;
        foreach (var camera in await cameras.ListAsync(onlyActive: true, ct))
        {
            if (!camera.IsCycleDue(now)) continue;
            var newFrames = await frames.CountNewAsync(camera.Id, ct);
            if (!camera.HasEnoughFrames(newFrames)) continue;

            camera.MarkCycleRequested(now);
            await events.PublishAsync(new AdaptationRequested(camera.Id, newFrames), ct);
            requested++;
        }

        if (requested > 0) await uow.CommitAsync(ct);
        return requested;
    }
}
