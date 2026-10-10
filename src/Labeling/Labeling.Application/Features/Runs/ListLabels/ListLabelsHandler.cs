using Labeling.Application.Abstractions;
using Labeling.Application.Models;
using Labeling.Domain.Labels;
using MediatR;

namespace Labeling.Application.Features.Runs.ListLabels;

public sealed class ListLabelsHandler(ILabelRepository labels) : IRequestHandler<ListLabelsQuery, IReadOnlyList<LabelDto>>
{
    public async Task<IReadOnlyList<LabelDto>> Handle(ListLabelsQuery q, CancellationToken ct)
    {
        LabelStatus? status = Enum.TryParse<LabelStatus>(q.Status, true, out var s) ? s : null;
        return (await labels.ListByRunAsync(q.RunId, status, Math.Max(0, q.Skip), Math.Clamp(q.Take, 1, 500), ct))
            .Select(LabelDto.From).ToList();
    }
}
