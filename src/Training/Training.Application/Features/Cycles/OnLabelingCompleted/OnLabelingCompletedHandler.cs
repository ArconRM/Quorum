using MediatR;
using Quorum.Application.Abstractions;
using Training.Application.Abstractions;
using Training.Application.Messages;
using Training.Application.Sagas;
using Training.Domain.Cycles;
using Training.Domain.Jobs;

namespace Training.Application.Features.Cycles.OnLabelingCompleted;

/// <summary>Шаг 2 → 3: разметка готова — ставим обучение студента (или проваливаем цикл, если меток мало).</summary>
public sealed class OnLabelingCompletedHandler(
    ICycleRepository cycles,
    ITrainingJobRepository jobs,
    IModelVersionRepository versions,
    IAdaptationSettings settings,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<OnLabelingCompletedCommand>
{
    public async Task Handle(OnLabelingCompletedCommand cmd, CancellationToken ct)
    {
        var cycle = await cycles.GetAsync(cmd.CycleId, ct);
        if (cycle is not { Status: CycleStatus.Labeling }) return;

        var compensation = cycle.OnLabelingCompleted(cmd.RunId, cmd.Accepted, settings.MinTrainingLabels);
        if (compensation is not null)
        {
            await Compensations.PublishAsync(events, cycle, compensation, ct);
        }
        else
        {
            var published = await versions.GetPublishedAsync(cycle.CameraId, ct);
            var job = TrainingJob.Create(cycle.Id, cycle.CameraId, cmd.RunId, cycle.Task!.Value, settings.Epochs,
                published?.Id, published?.ArtifactPath);
            jobs.Add(job);
            cycle.OnTrainingStarted(job.Id);
            await events.PublishAsync(new RunTrainingJob(job.Id), ct);
        }

        await uow.CommitAsync(ct);
    }
}
