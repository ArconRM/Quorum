using MediatR;
using Quorum.Core.Auth;
using Training.Application.Features.Cycles.GetCycle;
using Training.Application.Features.Cycles.ListCycles;
using Training.Application.Features.Cycles.StartCycle;

namespace Training.Service.Endpoints;

public sealed record StartCycleRequest(Guid CameraId, string? ValidationMode, double? AgreementThreshold);

public static class CycleEndpoints
{
    public static IEndpointRouteBuilder MapCycleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cycles").WithTags("Adaptation cycles").RequireAuthorization();

        // Запуск цикла вручную. 202: дальше его ведёт сага, статус — через GET /cycles/{id}.
        group.MapPost("/", async (StartCycleRequest req, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new StartCycleCommand(req.CameraId, Scheduled: false, req.ValidationMode, req.AgreementThreshold), ct);
            return Results.Accepted($"/cycles/{id}", new { id, status = "Snapshotting" });
        }).RequireAuthorization(p => p.RequireRole(Roles.Operator, Roles.Admin));

        group.MapGet("/", async (Guid? cameraId, int? take, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListCyclesQuery(cameraId, take ?? 50), ct)));

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetCycleQuery(id), ct)));

        return app;
    }
}
