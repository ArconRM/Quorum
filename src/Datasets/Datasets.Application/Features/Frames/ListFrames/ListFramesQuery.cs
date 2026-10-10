using Datasets.Application.Models;
using MediatR;

namespace Datasets.Application.Features.Frames.ListFrames;

public sealed record ListFramesQuery(Guid CameraId, int Skip, int Take) : IRequest<IReadOnlyList<FrameDto>>;
