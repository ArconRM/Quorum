using Labeling.Application.Models;
using MediatR;

namespace Labeling.Application.Features.Reviews.SubmitReview;

/// <summary>Решение разметчика: Approve / Correct (с новыми боксами или текстом) / Reject.</summary>
public sealed record SubmitReviewCommand(Guid TaskId, Guid AnnotatorId, string Verdict, IReadOnlyList<BoxDto>? Boxes, string? Text)
    : IRequest<LabelDto>;
