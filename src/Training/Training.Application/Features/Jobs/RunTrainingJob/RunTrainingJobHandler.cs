using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Exceptions;
using Quorum.Domain;
using Training.Application.Abstractions;
using Training.Application.Messages;
using Training.Application.Sagas;
using Training.Domain.Cycles;
using Training.Domain.Jobs;
using Training.Domain.Models;

namespace Training.Application.Features.Jobs.RunTrainingJob;

/// <summary>
/// Шаг 3: обучающая выборка из Labeling (gRPC) → обучение на воркере (gRPC-стрим эпох) → версия-кандидат.
/// Если воркер недоступен — цикл проваливается, разметка и снимок откатываются.
/// </summary>
public sealed class RunTrainingJobHandler(
    ITrainingJobRepository jobs,
    ICycleRepository cycles,
    IModelVersionRepository versions,
    ILabeledDataSource dataSource,
    ITrainingWorker worker,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<RunTrainingJobCommand>
{
    public async Task Handle(RunTrainingJobCommand cmd, CancellationToken ct)
    {
        var job = await jobs.GetAsync(cmd.JobId, ct);
        if (job is not { Status: TrainingJobStatus.Queued }) return;
        var cycle = await cycles.GetAsync(job.CycleId, ct);
        if (cycle is not { Status: CycleStatus.Training }) return;

        try
        {
            var samples = await dataSource.GetAcceptedAsync(job.LabelingRunId, ct);
            var trainSet = samples.Where(s => !s.IsHoldout).ToList();
            job.Start(trainSet.Count);
            await uow.CommitAsync(ct);

            var result = await worker.TrainAsync(job, trainSet, async (epoch, loss) =>
            {
                job.ReportProgress(epoch, loss);
                await uow.CommitAsync(ct); // прогресс виден в GET /jobs/{id}
            }, ct);

            job.Succeed(result.ArtifactPath, result.MetricName, result.MetricValue);
            var candidate = ModelVersion.CreateCandidate(job, await versions.NextNumberAsync(job.CameraId, ct));
            versions.Add(candidate);
            cycle.OnTrainingSucceeded(candidate.Id);
            await events.PublishAsync(new EvaluateCandidate(cycle.Id), ct);
        }
        catch (Exception ex) when (ex is ExternalServiceException or DomainException)
        {
            if (job.Status is TrainingJobStatus.Queued or TrainingJobStatus.Running) job.Fail(ex.Message);
            var compensation = cycle.OnTrainingFailed(ex.Message);
            await Compensations.PublishAsync(events, cycle, compensation, ct);
        }

        await uow.CommitAsync(ct);
    }
}
