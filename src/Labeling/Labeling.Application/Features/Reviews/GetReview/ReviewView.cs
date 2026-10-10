using Labeling.Application.Abstractions;
using Labeling.Application.Features.Runs.GetFramePredictions;
using Labeling.Application.Models;
using Labeling.Domain.Reviews;
using Quorum.Application.Exceptions;

namespace Labeling.Application.Features.Reviews.GetReview;

internal static class ReviewView
{
    public static async Task<ReviewDto> BuildAsync(ReviewTask task, ILabelRepository labels, IRunRepository runs,
        IPredictionRepository predictions, CancellationToken ct)
    {
        var label = await labels.GetAsync(task.LabelId, ct) ?? throw new NotFoundException($"Label {task.LabelId} not found");
        var run = await runs.GetAsync(task.RunId, ct) ?? throw new NotFoundException($"Run {task.RunId} not found");
        var answers = await PredictionView.ForFrameAsync(run, task.FrameId, predictions, ct);
        return new ReviewDto(task.Id, task.RunId, run.Task.ToString(), task.Status.ToString(), LabelDto.From(label), answers);
    }
}
