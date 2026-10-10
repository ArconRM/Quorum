using Labeling.Application.Abstractions;
using Labeling.Application.Models;
using MediatR;
using Quorum.Application.Exceptions;

namespace Labeling.Application.Features.Runs.GetFramePredictions;

public sealed class GetFramePredictionsHandler(IRunRepository runs, IPredictionRepository predictions)
    : IRequestHandler<GetFramePredictionsQuery, IReadOnlyList<PredictionDto>>
{
    public async Task<IReadOnlyList<PredictionDto>> Handle(GetFramePredictionsQuery q, CancellationToken ct)
    {
        var run = await runs.GetAsync(q.RunId, ct) ?? throw new NotFoundException($"Run {q.RunId} not found");
        return await PredictionView.ForFrameAsync(run, q.FrameId, predictions, ct);
    }
}
