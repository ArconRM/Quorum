using Labeling.Application.Abstractions;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;

namespace Labeling.Application.Features.Runs.DiscardLabelingRun;

public sealed class DiscardLabelingRunHandler(
    IRunRepository runs,
    IReviewTaskRepository reviews,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<DiscardLabelingRunCommand>
{
    public async Task Handle(DiscardLabelingRunCommand cmd, CancellationToken ct)
    {
        var run = await runs.GetByCycleAsync(cmd.CycleId, ct);
        if (run is null) return; // прогон так и не был создан — компенсировать нечего

        if (run.Discard())
            foreach (var task in await reviews.ListUnfinishedByRunAsync(run.Id, ct)) task.Cancel();

        await events.PublishAsync(new LabelingDiscarded(cmd.CycleId, run.Id), ct);
        await uow.CommitAsync(ct);
    }
}
