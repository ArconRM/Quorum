using MediatR;

namespace Datasets.Application.Features.Frames.GetFrameContent;

public sealed record FrameContent(Stream Content, string ContentType);

/// <summary>Файл кадра — например, для разметчика при ручной проверке.</summary>
public sealed record GetFrameContentQuery(Guid FrameId) : IRequest<FrameContent>;
