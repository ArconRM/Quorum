using MediatR;
using Training.Application.Abstractions;
using Training.Application.Models;

namespace Training.Application.Features.Cycles.ListCycles;

public sealed class ListCyclesHandler(ICycleRepository cycles) : IRequestHandler<ListCyclesQuery, IReadOnlyList<CycleDto>>
{
    public async Task<IReadOnlyList<CycleDto>> Handle(ListCyclesQuery q, CancellationToken ct) =>
        (await cycles.ListAsync(q.CameraId, Math.Clamp(q.Take, 1, 200), ct)).Select(c => CycleDto.From(c)).ToList();
}
