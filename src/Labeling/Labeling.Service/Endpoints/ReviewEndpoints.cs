using System.Security.Claims;
using Labeling.Application.Features.Reviews.ClaimNextReview;
using Labeling.Application.Features.Reviews.GetReview;
using Labeling.Application.Features.Reviews.SubmitReview;
using Labeling.Application.Models;
using MediatR;
using Quorum.Core.Auth;

namespace Labeling.Service.Endpoints;

public sealed record SubmitReviewRequest(string Verdict, IReadOnlyList<BoxDto>? Boxes, string? Text);

public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reviews").WithTags("Reviews")
            .RequireAuthorization(p => p.RequireRole(Roles.Annotator, Roles.Admin));

        // Взять следующее задание: 200 + задание или 204, если проверять нечего.
        group.MapPost("/next", async (ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            await sender.Send(new ClaimNextReviewCommand(user.GetUserId()), ct) is { } review
                ? Results.Ok(review)
                : Results.NoContent());

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetReviewQuery(id), ct)));

        group.MapPost("/{id:guid}", async (Guid id, SubmitReviewRequest req, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new SubmitReviewCommand(id, user.GetUserId(), req.Verdict, req.Boxes, req.Text), ct)));

        return app;
    }
}
