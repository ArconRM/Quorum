using Labeling.Application.Abstractions;
using Labeling.Application.Models;
using Labeling.Domain.Runs;

namespace Labeling.Application.Features.Runs.GetFramePredictions;

internal static class PredictionView
{
    public static async Task<IReadOnlyList<PredictionDto>> ForFrameAsync(
        LabelingRun run, Guid frameId, IPredictionRepository predictions, CancellationToken ct)
    {
        var teachers = run.Teachers.ToDictionary(t => t.TeacherId);
        return (await predictions.ListByFrameAsync(run.Id, frameId, ct))
            .Select(p => new PredictionDto(p.TeacherId,
                teachers.TryGetValue(p.TeacherId, out var t) ? t.Name : "?",
                teachers.TryGetValue(p.TeacherId, out var tw) ? tw.Weight : 0,
                p.Boxes.Select(BoxDto.From).ToList(), p.Text, p.Confidence))
            .OrderBy(p => p.Teacher)
            .ToList();
    }
}
