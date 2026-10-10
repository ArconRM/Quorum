using Datasets.Application.Models;
using MediatR;

namespace Datasets.Application.Features.Cameras.GetCamera;

public sealed record GetCameraQuery(Guid CameraId) : IRequest<CameraDto>;
