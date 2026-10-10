using Labeling.Application.Abstractions;
using Labeling.Application.Models;
using Labeling.Domain.Labels;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;
using Quorum.Application.Exceptions;

namespace Labeling.Application.Features.Reviews.SubmitReview;

public sealed class SubmitReviewHandler(
    IReviewTaskRepository reviews,
    ILabelRepository labels,
    IRunRepository runs,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<SubmitReviewCommand, LabelDto>
{
    public async Task<LabelDto> Handle(SubmitReviewCommand cmd, CancellationToken ct)
    {
        var task = await reviews.GetAsync(cmd.TaskId, ct) ?? throw new NotFoundException($"Review task {cmd.TaskId} not found");
        var label = await labels.GetAsync(task.LabelId, ct) ?? throw new NotFoundException($"Label {task.LabelId} not found");
        var run = await runs.GetAsync(task.RunId, ct) ?? throw new NotFoundException($"Run {task.RunId} not found");

        var verdict = Enum.Parse<ReviewVerdict>(cmd.Verdict, ignoreCase: true);
        task.Complete(cmd.AnnotatorId, verdict);
        label.ApplyReview(cmd.AnnotatorId, verdict, cmd.Boxes?.Select(b => b.ToDomain()).ToList(), cmd.Text);

        // Счётчик ожидающих проверок живёт в прогоне (xmin): последний разметчик закрывает прогон ровно один раз.
        if (run.RecordReview(label.Status))
            await events.PublishAsync(new LabelingCompleted(run.CycleId, run.Id, run.Accepted, run.Rejected, run.Reviewed), ct);

        await uow.CommitAsync(ct);
        return LabelDto.From(label);
    }
}
