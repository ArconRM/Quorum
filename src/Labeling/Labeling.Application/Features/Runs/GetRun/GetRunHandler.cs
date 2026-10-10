using Labeling.Application.Abstractions;
using Labeling.Application.Models;
using MediatR;
using Quorum.Application.Exceptions;

namespace Labeling.Application.Features.Runs.GetRun;

public sealed class GetRunHandler(IRunRepository runs) : IRequestHandler<GetRunQuery, RunDto>
{
    public async Task<RunDto> Handle(GetRunQuery q, CancellationToken ct)
    {
        var run = q.RunId is { } id ? await runs.GetAsync(id, ct)
            : q.CycleId is { } cycleId ? await runs.GetByCycleAsync(cycleId, ct)
            : null;
        return RunDto.From(run ?? throw new NotFoundException("Labeling run not found"));
    }
}
