using Datasets.Application.Abstractions;
using Datasets.Application.Models;
using MediatR;

namespace Datasets.Application.Features.Frames.ListFrames;

public sealed class ListFramesHandler(IFrameRepository frames) : IRequestHandler<ListFramesQuery, IReadOnlyList<FrameDto>>
{
    public async Task<IReadOnlyList<FrameDto>> Handle(ListFramesQuery q, CancellationToken ct) =>
        (await frames.ListByCameraAsync(q.CameraId, Math.Max(0, q.Skip), Math.Clamp(q.Take, 1, 500), ct))
        .Select(FrameDto.From).ToList();
}
