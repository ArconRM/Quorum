using Datasets.Application.Abstractions;
using Datasets.Application.Models;
using Datasets.Domain.Frames;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Exceptions;

namespace Datasets.Application.Features.Frames.UploadFrame;

public sealed class UploadFrameHandler(
    ICameraRepository cameras,
    IFrameRepository frames,
    IFrameStorage storage,
    IUnitOfWork uow) : IRequestHandler<UploadFrameCommand, FrameDto>
{
    public async Task<FrameDto> Handle(UploadFrameCommand cmd, CancellationToken ct)
    {
        var camera = await cameras.GetAsync(cmd.CameraId, ct) ?? throw new NotFoundException($"Camera {cmd.CameraId} not found");
        camera.EnsureCanAcceptFrames();

        var frameId = Guid.NewGuid();
        var extension = cmd.ContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bmp",
        };

        // Файл пишется до коммита: в худшем случае останется «осиротевший» файл, но не запись без файла.
        var path = await storage.SaveAsync(camera.Id, frameId, extension, cmd.Content, ct);
        var frame = Frame.Create(frameId, camera.Id, path, cmd.ContentType, cmd.SizeBytes, cmd.CapturedAt);
        frames.Add(frame);
        await uow.CommitAsync(ct);
        return FrameDto.From(frame);
    }
}
