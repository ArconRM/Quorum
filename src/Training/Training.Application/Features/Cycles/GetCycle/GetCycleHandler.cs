using MediatR;
using Quorum.Application.Exceptions;
using Training.Application.Abstractions;
using Training.Application.Models;

namespace Training.Application.Features.Cycles.GetCycle;

public sealed class GetCycleHandler(ICycleRepository cycles, IEvaluationRepository evaluations) : IRequestHandler<GetCycleQuery, CycleDto>
{
    public async Task<CycleDto> Handle(GetCycleQuery q, CancellationToken ct)
    {
        var cycle = await cycles.GetAsync(q.CycleId, ct) ?? throw new NotFoundException($"Cycle {q.CycleId} not found");
        return CycleDto.From(cycle, await evaluations.GetByCycleAsync(cycle.Id, ct));
    }
}
