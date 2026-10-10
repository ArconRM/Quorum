using MediatR;
using Training.Application.Abstractions;
using Training.Application.Models;

namespace Training.Application.Features.Models.ListModelVersions;

public sealed class ListModelVersionsHandler(IModelVersionRepository versions)
    : IRequestHandler<ListModelVersionsQuery, IReadOnlyList<ModelVersionDto>>
{
    public async Task<IReadOnlyList<ModelVersionDto>> Handle(ListModelVersionsQuery q, CancellationToken ct) =>
        (await versions.ListByCameraAsync(q.CameraId, ct)).Select(ModelVersionDto.From).ToList();
}
