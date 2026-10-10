using Microsoft.EntityFrameworkCore;
using Training.Application.Abstractions;
using Training.Domain.Evaluations;
using Training.Infrastructure.Db;

namespace Training.Infrastructure.Repositories;

internal sealed class EvaluationRepository(TrainingDbContext db) : IEvaluationRepository
{
    public Task<Evaluation?> GetByCycleAsync(Guid cycleId, CancellationToken ct) =>
        db.Evaluations.AsNoTracking().FirstOrDefaultAsync(e => e.CycleId == cycleId, ct);

    public void Add(Evaluation evaluation) => db.Evaluations.Add(evaluation);
}
