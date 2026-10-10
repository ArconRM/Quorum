using Labeling.Application.Models;
using MediatR;

namespace Labeling.Application.Features.Reviews.GetReview;

public sealed record GetReviewQuery(Guid TaskId) : IRequest<ReviewDto>;
