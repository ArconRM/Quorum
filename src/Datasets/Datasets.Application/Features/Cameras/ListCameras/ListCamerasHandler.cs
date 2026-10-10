using Datasets.Application.Abstractions;
using Datasets.Application.Models;
using MediatR;

namespace Datasets.Application.Features.Cameras.ListCameras;

public sealed class ListCamerasHandler(ICameraRepository cameras, IFrameRepository frames)
    : IRequestHandler<ListCamerasQuery, IReadOnlyList<CameraDto>>
{
    public async Task<IReadOnlyList<CameraDto>> Handle(ListCamerasQuery q, CancellationToken ct)
    {
        var result = new List<CameraDto>();
        foreach (var camera in await cameras.ListAsync(onlyActive: false, ct))
            result.Add(CameraDto.From(camera, await frames.CountNewAsync(camera.Id, ct)));
        return result;
    }
}
