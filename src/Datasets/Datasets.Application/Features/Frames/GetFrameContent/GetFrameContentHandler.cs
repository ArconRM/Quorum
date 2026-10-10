using Datasets.Application.Abstractions;
using MediatR;
using Quorum.Application.Exceptions;

namespace Datasets.Application.Features.Frames.GetFrameContent;

public sealed class GetFrameContentHandler(IFrameRepository frames, IFrameStorage storage)
    : IRequestHandler<GetFrameContentQuery, FrameContent>
{
    public async Task<FrameContent> Handle(GetFrameContentQuery q, CancellationToken ct)
    {
        var frame = await frames.GetAsync(q.FrameId, ct) ?? throw new NotFoundException($"Frame {q.FrameId} not found");
        return new FrameContent(storage.OpenRead(frame.StoragePath), frame.ContentType);
    }
}
