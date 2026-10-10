using Datasets.Application.Features.Snapshots.GetSnapshot;
using Datasets.Application.Features.Snapshots.GetSnapshotFrames;
using MediatR;

namespace Datasets.Service.Endpoints;

public static class SnapshotEndpoints
{
    public static IEndpointRouteBuilder MapSnapshotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/snapshots").WithTags("Snapshots").RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSnapshotQuery(id), ct)));

        group.MapGet("/{id:guid}/frames", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSnapshotFramesQuery(id), ct)));

        return app;
    }
}
