using Labeling.Application.Features.Runs.GetFramePredictions;
using Labeling.Application.Features.Runs.GetRun;
using Labeling.Application.Features.Runs.ListLabels;
using MediatR;

namespace Labeling.Service.Endpoints;

public static class RunEndpoints
{
    public static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/runs").WithTags("Runs").RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetRunQuery(id, null), ct)));

        // GET /runs?cycleId=... — прогон конкретного цикла адаптации
        group.MapGet("/", async (Guid cycleId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetRunQuery(null, cycleId), ct)));

        group.MapGet("/{id:guid}/labels", async (Guid id, string? status, int? skip, int? take, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListLabelsQuery(id, status, skip ?? 0, take ?? 100), ct)));

        group.MapGet("/{id:guid}/frames/{frameId:guid}/predictions", async (Guid id, Guid frameId, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetFramePredictionsQuery(id, frameId), ct)));

        return app;
    }
}
