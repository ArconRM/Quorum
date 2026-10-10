using Datasets.Application.Models;
using MediatR;

namespace Datasets.Application.Features.Frames.UploadFrame;

/// <summary>Загрузка кадра с камеры / мобильного клиента.</summary>
public sealed record UploadFrameCommand(Guid CameraId, string ContentType, long SizeBytes, Stream Content, DateTime? CapturedAt)
    : IRequest<FrameDto>;
