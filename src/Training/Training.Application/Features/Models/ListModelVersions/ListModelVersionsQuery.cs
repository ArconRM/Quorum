using MediatR;
using Training.Application.Models;

namespace Training.Application.Features.Models.ListModelVersions;

public sealed record ListModelVersionsQuery(Guid CameraId) : IRequest<IReadOnlyList<ModelVersionDto>>;
