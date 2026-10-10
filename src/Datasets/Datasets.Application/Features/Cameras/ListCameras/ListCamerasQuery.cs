using Datasets.Application.Models;
using MediatR;

namespace Datasets.Application.Features.Cameras.ListCameras;

public sealed record ListCamerasQuery : IRequest<IReadOnlyList<CameraDto>>;
