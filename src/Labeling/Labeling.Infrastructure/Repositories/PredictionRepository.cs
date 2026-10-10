using Labeling.Application.Abstractions;
using Labeling.Domain.Predictions;
using Labeling.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace Labeling.Infrastructure.Repositories;

internal sealed class PredictionRepository(LabelingDbContext db) : IPredictionRepository
{
    public async Task<IReadOnlyList<Prediction>> ListByRunAsync(Guid runId, CancellationToken ct) =>
        await db.Predictions.AsNoTracking().Where(p => p.RunId == runId).ToListAsync(ct);

    public async Task<IReadOnlyList<Prediction>> ListByFrameAsync(Guid runId, Guid frameId, CancellationToken ct) =>
        await db.Predictions.AsNoTracking().Where(p => p.RunId == runId && p.FrameId == frameId).ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> FramesWithPredictionsAsync(Guid runId, Guid teacherId, CancellationToken ct) =>
        (await db.Predictions.Where(p => p.RunId == runId && p.TeacherId == teacherId).Select(p => p.FrameId).ToListAsync(ct))
        .ToHashSet();

    public void Add(Prediction prediction) => db.Predictions.Add(prediction);
}
