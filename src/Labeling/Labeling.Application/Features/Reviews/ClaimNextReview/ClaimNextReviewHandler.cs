using Labeling.Application.Abstractions;
using Labeling.Application.Features.Reviews.GetReview;
using Labeling.Application.Models;
using MediatR;
using Quorum.Application.Abstractions;

namespace Labeling.Application.Features.Reviews.ClaimNextReview;

public sealed class ClaimNextReviewHandler(
    IReviewTaskRepository reviews,
    ILabelRepository labels,
    IRunRepository runs,
    IPredictionRepository predictions,
    IUnitOfWork uow) : IRequestHandler<ClaimNextReviewCommand, ReviewDto?>
{
    public async Task<ReviewDto?> Handle(ClaimNextReviewCommand cmd, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // Сначала — своё недоделанное задание (обновил страницу — не потерял работу).
        var task = await reviews.FindClaimedByAsync(cmd.AnnotatorId, ct) ?? await reviews.NextAvailableAsync(now, ct);
        if (task is null) return null;

        task.Claim(cmd.AnnotatorId, now);
        await uow.CommitAsync(ct); // при гонке двух разметчиков второй получит 409 (xmin) и повторит запрос
        return await ReviewView.BuildAsync(task, labels, runs, predictions, ct);
    }
}
