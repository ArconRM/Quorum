using Labeling.Application.Abstractions;
using Labeling.Domain.Common;
using Labeling.Domain.Consensus;
using Labeling.Domain.Labels;
using Labeling.Domain.Predictions;
using Labeling.Domain.Reviews;
using Labeling.Domain.Runs;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;
using Quorum.Application.Exceptions;
using Quorum.Domain;

namespace Labeling.Application.Features.Runs.ProcessLabelingRun;

/// <summary>
/// Ядро разметки:
/// 1. берём состав снимка из Datasets (gRPC);
/// 2. каждого учителя прогоняем через GPU-воркер (gRPC, поток предсказаний), всё сохраняем как кэш;
/// 3. по каждому кадру считаем консенсус и по стратегии решаем: принять / отбросить / человеку.
/// Учитель, уже ответивший на кадр, при повторном запуске его пропускает — сбой посередине не теряет работу.
/// </summary>
public sealed class ProcessLabelingRunHandler(
    IRunRepository runs,
    IPredictionRepository predictions,
    ILabelRepository labels,
    IReviewTaskRepository reviews,
    IFrameSource frameSource,
    ITeacherRunner teacherRunner,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<ProcessLabelingRunCommand>
{
    private const int CommitEvery = 100;

    public async Task Handle(ProcessLabelingRunCommand cmd, CancellationToken ct)
    {
        var run = await runs.GetAsync(cmd.RunId, ct);
        if (run is not { Status: RunStatus.Running }) return;

        try
        {
            var frames = await frameSource.GetSnapshotFramesAsync(run.SnapshotId, ct);
            run.SetTotalFrames(frames.Count);

            foreach (var teacher in run.Teachers.Where(t => !t.IsCompleted))
                await RunTeacherAsync(run, teacher, frames, ct);

            await AggregateAsync(run, frames, ct);
        }
        catch (Exception ex) when (ex is ExternalServiceException or DomainException)
        {
            run.Fail(ex.Message);
            await events.PublishAsync(new LabelingFailed(run.CycleId, run.Id, ex.Message), ct);
        }

        await uow.CommitAsync(ct);
    }

    private async Task RunTeacherAsync(LabelingRun run, RunTeacher teacher, IReadOnlyList<SourceFrame> frames, CancellationToken ct)
    {
        var done = await predictions.FramesWithPredictionsAsync(run.Id, teacher.TeacherId, ct);
        var pending = frames.Where(f => !done.Contains(f.FrameId)).ToList();
        var known = frames.Select(f => f.FrameId).ToHashSet();
        var processed = done.Count;

        await foreach (var p in teacherRunner.RunAsync(teacher, run.Task, run.Prompt, pending, ct))
        {
            if (!known.Contains(p.FrameId) || done.Contains(p.FrameId)) continue;
            predictions.Add(Prediction.Create(run.Id, p.FrameId, teacher.TeacherId, p.Boxes, p.Text, p.Confidence));
            processed++;
            if (processed % CommitEvery == 0)
            {
                run.RecordProgress(teacher.TeacherId, processed);
                await uow.CommitAsync(ct);
            }
        }

        run.CompleteTeacher(teacher.TeacherId);
        await uow.CommitAsync(ct);
    }

    private async Task AggregateAsync(LabelingRun run, IReadOnlyList<SourceFrame> frames, CancellationToken ct)
    {
        var byFrame = (await predictions.ListByRunAsync(run.Id, ct)).ToLookup(p => p.FrameId);
        int accepted = 0, rejected = 0, needsReview = 0;

        foreach (var frame in frames)
        {
            var answers = byFrame[frame.FrameId].ToDictionary(p => p.TeacherId);
            // Учитель без ответа на кадр голосует «ничего нет» — молчание не должно завышать согласие.
            var votes = run.Teachers.Select(t => answers.TryGetValue(t.TeacherId, out var p)
                    ? new TeacherVote(t.TeacherId, t.Weight, p.Boxes, p.Text)
                    : new TeacherVote(t.TeacherId, t.Weight, [], null))
                .ToList();

            var consensus = run.Task == TaskType.Detection ? BoxConsensus.Compute(votes) : TextConsensus.Compute(votes);
            var status = run.Strategy.Decide(consensus.Agreement);
            var label = ConsensusLabel.Create(run.Id, frame.FrameId, frame.Path, frame.Split, run.Task, consensus, status);
            labels.Add(label);

            switch (status)
            {
                case LabelStatus.Accepted: accepted++; break;
                case LabelStatus.Rejected: rejected++; break;
                default:
                    needsReview++;
                    reviews.Add(ReviewTask.For(label));
                    break;
            }
        }

        run.FinishAggregation(accepted, rejected, needsReview);
        if (run.Status == RunStatus.Completed)
            await events.PublishAsync(new LabelingCompleted(run.CycleId, run.Id, run.Accepted, run.Rejected, run.Reviewed), ct);
    }
}
