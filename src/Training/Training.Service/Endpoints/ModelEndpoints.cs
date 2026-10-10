using MediatR;
using Quorum.Core.Auth;
using Training.Application.Features.Jobs.GetJob;
using Training.Application.Features.Models.GetPublishedModel;
using Training.Application.Features.Models.ListModelVersions;
using Training.Application.Features.Models.RollbackModel;

namespace Training.Service.Endpoints;

public static class ModelEndpoints
{
    public static IEndpointRouteBuilder MapModelEndpoints(this IEndpointRouteBuilder app)
    {
        // Какую модель запускать на камере — спрашивает устройство. Ответ из кеша Redis.
        app.MapGet("/cameras/{cameraId:guid}/model", async (Guid cameraId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPublishedModelQuery(cameraId), ct) is { } model ? Results.Ok(model) : Results.NotFound())
            .WithTags("Models")
            .RequireAuthorization();

        app.MapGet("/cameras/{cameraId:guid}/models", async (Guid cameraId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListModelVersionsQuery(cameraId), ct)))
            .WithTags("Models")
            .RequireAuthorization();

        app.MapPost("/models/{id:guid}/rollback", async (Guid id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new RollbackModelCommand(id), ct)))
            .WithTags("Models")
            .RequireAuthorization(p => p.RequireRole(Roles.Operator, Roles.Admin));

        app.MapGet("/jobs/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetJobQuery(id), ct)))
            .WithTags("Training jobs")
            .RequireAuthorization();

        return app;
    }
}
