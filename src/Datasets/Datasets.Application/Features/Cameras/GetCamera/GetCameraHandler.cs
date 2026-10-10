using Datasets.Application.Abstractions;
using Datasets.Application.Models;
using MediatR;
using Quorum.Application.Exceptions;

namespace Datasets.Application.Features.Cameras.GetCamera;

public sealed class GetCameraHandler(ICameraRepository cameras, IFrameRepository frames) : IRequestHandler<GetCameraQuery, CameraDto>
{
    public async Task<CameraDto> Handle(GetCameraQuery q, CancellationToken ct)
    {
        var camera = await cameras.GetAsync(q.CameraId, ct) ?? throw new NotFoundException($"Camera {q.CameraId} not found");
        return CameraDto.From(camera, await frames.CountNewAsync(camera.Id, ct));
    }
}
