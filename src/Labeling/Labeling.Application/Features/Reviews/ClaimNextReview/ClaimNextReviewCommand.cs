using Labeling.Application.Models;
using MediatR;

namespace Labeling.Application.Features.Reviews.ClaimNextReview;

/// <summary>Разметчик берёт следующее задание. Null — проверять нечего.</summary>
public sealed record ClaimNextReviewCommand(Guid AnnotatorId) : IRequest<ReviewDto?>;
