using Datasets.Application.Features.Cameras.CreateCamera;
using Datasets.Application.Features.Cameras.GetCamera;
using Datasets.Application.Features.Cameras.ListCameras;
using Datasets.Application.Features.Cameras.UpdateCamera;
using MediatR;
using Quorum.Core.Auth;

namespace Datasets.Service.Endpoints;

public sealed record UpdateCameraRequest(string? Prompt, int? MinFramesPerCycle, int? CycleIntervalHours, bool? IsActive);

public static class CameraEndpoints
{
    public static IEndpointRouteBuilder MapCameraEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cameras").WithTags("Cameras").RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListCamerasQuery(), ct)));

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetCameraQuery(id), ct)));

        group.MapPost("/", async (CreateCameraCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(cmd, ct);
            return Results.Created($"/cameras/{id}", new { id });
        }).RequireAuthorization(p => p.RequireRole(Roles.Operator, Roles.Admin));

        group.MapPatch("/{id:guid}", async (Guid id, UpdateCameraRequest req, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(
                new UpdateCameraCommand(id, req.Prompt, req.MinFramesPerCycle, req.CycleIntervalHours, req.IsActive), ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Operator, Roles.Admin));

        return app;
    }
}
