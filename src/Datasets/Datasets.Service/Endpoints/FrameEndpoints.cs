using Datasets.Application.Features.Frames.GetFrameContent;
using Datasets.Application.Features.Frames.ListFrames;
using Datasets.Application.Features.Frames.UploadFrame;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Quorum.Core.Auth;

namespace Datasets.Service.Endpoints;

public static class FrameEndpoints
{
    public static IEndpointRouteBuilder MapFrameEndpoints(this IEndpointRouteBuilder app)
    {
        // Загрузка кадра: multipart/form-data, поле file (+ необязательное capturedAt).
        app.MapPost("/cameras/{cameraId:guid}/frames", async (
                Guid cameraId, IFormFile file, [FromForm] DateTime? capturedAt, ISender sender, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                var frame = await sender.Send(new UploadFrameCommand(cameraId, file.ContentType, file.Length, stream, capturedAt), ct);
                return Results.Created($"/frames/{frame.Id}", frame);
            })
            .WithTags("Frames")
            .DisableAntiforgery() // API для устройств и скриптов, не для браузерных форм
            .RequireAuthorization(p => p.RequireRole(Roles.Operator, Roles.Admin));

        app.MapGet("/cameras/{cameraId:guid}/frames", async (Guid cameraId, int? skip, int? take, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListFramesQuery(cameraId, skip ?? 0, take ?? 50), ct)))
            .WithTags("Frames")
            .RequireAuthorization();

        // Разметчик смотрит кадр при ручной проверке.
        app.MapGet("/frames/{id:guid}/content", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                var content = await sender.Send(new GetFrameContentQuery(id), ct);
                return Results.Stream(content.Content, content.ContentType);
            })
            .WithTags("Frames")
            .RequireAuthorization();

        return app;
    }
}
