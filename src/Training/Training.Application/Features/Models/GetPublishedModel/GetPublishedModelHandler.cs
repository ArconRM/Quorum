using MediatR;
using Training.Application.Abstractions;
using Training.Application.Models;

namespace Training.Application.Features.Models.GetPublishedModel;

public sealed class GetPublishedModelHandler(IPublishedModelCache cache) : IRequestHandler<GetPublishedModelQuery, ModelVersionDto?>
{
    public Task<ModelVersionDto?> Handle(GetPublishedModelQuery q, CancellationToken ct) => cache.GetAsync(q.CameraId, ct);
}
