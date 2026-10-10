using Labeling.Application.Abstractions;
using Labeling.Application.Models;
using MediatR;
using Quorum.Application.Exceptions;

namespace Labeling.Application.Features.Reviews.GetReview;

public sealed class GetReviewHandler(
    IReviewTaskRepository reviews,
    ILabelRepository labels,
    IRunRepository runs,
    IPredictionRepository predictions) : IRequestHandler<GetReviewQuery, ReviewDto>
{
    public async Task<ReviewDto> Handle(GetReviewQuery q, CancellationToken ct)
    {
        var task = await reviews.GetAsync(q.TaskId, ct) ?? throw new NotFoundException($"Review task {q.TaskId} not found");
        return await ReviewView.BuildAsync(task, labels, runs, predictions, ct);
    }
}
