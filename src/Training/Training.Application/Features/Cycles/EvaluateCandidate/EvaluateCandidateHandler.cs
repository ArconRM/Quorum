using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;
using Quorum.Application.Exceptions;
using Quorum.Domain;
using Training.Application.Abstractions;
using Training.Application.Sagas;
using Training.Domain.Cycles;
using Training.Domain.Evaluations;
using Training.Domain.Models;

namespace Training.Application.Features.Cycles.EvaluateCandidate;

/// <summary>
/// Шаг 4: кандидат и текущая модель оцениваются на отложенной выборке этого цикла.
/// Лучше — публикуем (старая уходит в архив), иначе кандидат отклоняется. Снимок помечается использованным.
/// </summary>
public sealed class EvaluateCandidateHandler(
    ICycleRepository cycles,
    IModelVersionRepository versions,
    IEvaluationRepository evaluations,
    ILabeledDataSource dataSource,
    ITrainingWorker worker,
    IPublishedModelCache cache,
    IAdaptationSettings settings,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<EvaluateCandidateCommand>
{
    public async Task Handle(EvaluateCandidateCommand cmd, CancellationToken ct)
    {
        var cycle = await cycles.GetAsync(cmd.CycleId, ct);
        if (cycle is not { Status: CycleStatus.Evaluating }) return;

        var candidate = await versions.GetAsync(cycle.CandidateVersionId!.Value, ct)
                        ?? throw new NotFoundException($"Candidate version {cycle.CandidateVersionId} not found");
        var published = await versions.GetPublishedAsync(cycle.CameraId, ct);
        var promoted = false;

        try
        {
            var holdout = (await dataSource.GetAcceptedAsync(cycle.LabelingRunId!.Value, ct)).Where(s => s.IsHoldout).ToList();
            var scores = await worker.EvaluateAsync(candidate.Task.ToString(), candidate.ArtifactPath, published?.ArtifactPath, holdout, ct);

            var evaluation = Evaluation.Decide(cycle.Id, candidate.Id, published?.Id, scores.MetricName,
                scores.Candidate, published is null ? null : scores.Baseline, holdout.Count, settings.MinImprovement);
            evaluations.Add(evaluation);
            candidate.RecordEvaluation(scores.Candidate);

            promoted = evaluation.Decision == EvaluationDecision.Promote;
            if (promoted)
            {
                ModelPublication.Publish(candidate, published);
                await events.PublishAsync(new ModelPublished(candidate.CameraId, candidate.Id, candidate.Number,
                    candidate.MetricName, scores.Candidate), ct);
            }
            else
            {
                candidate.Reject();
            }

            cycle.Complete(promoted);
            await events.PublishAsync(new ReleaseSnapshot(cycle.Id, cycle.SnapshotId!.Value, Discard: false), ct);
        }
        catch (Exception ex) when (ex is ExternalServiceException or DomainException)
        {
            candidate.Reject();
            var compensation = cycle.OnEvaluationFailed(ex.Message);
            await Compensations.PublishAsync(events, cycle, compensation, ct);
        }

        await uow.CommitAsync(ct);
        // После коммита: иначе параллельный запрос успеет закешировать старую версию.
        if (promoted) await cache.InvalidateAsync(cycle.CameraId, ct);
    }
}
