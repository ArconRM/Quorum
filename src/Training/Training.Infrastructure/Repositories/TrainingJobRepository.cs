using Microsoft.EntityFrameworkCore;
using Training.Application.Abstractions;
using Training.Domain.Jobs;
using Training.Infrastructure.Db;

namespace Training.Infrastructure.Repositories;

internal sealed class TrainingJobRepository(TrainingDbContext db) : ITrainingJobRepository
{
    public Task<TrainingJob?> GetAsync(Guid id, CancellationToken ct) => db.Jobs.FirstOrDefaultAsync(j => j.Id == id, ct);
    public void Add(TrainingJob job) => db.Jobs.Add(job);
}
