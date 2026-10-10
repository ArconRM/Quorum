using Microsoft.EntityFrameworkCore;
using Quorum.Application.Abstractions;
using Quorum.Core.Persistence;
using Training.Domain.Cycles;
using Training.Domain.Evaluations;
using Training.Domain.Jobs;
using Training.Domain.Models;

namespace Training.Infrastructure.Db;

/// <summary>База сервиса Training (БД training): циклы адаптации (сага), обучение, версии моделей, оценки + outbox/inbox.</summary>
public sealed class TrainingDbContext(DbContextOptions<TrainingDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<AdaptationCycle> Cycles => Set<AdaptationCycle>();
    public DbSet<TrainingJob> Jobs => Set<TrainingJob>();
    public DbSet<ModelVersion> Versions => Set<ModelVersion>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();

    public Task CommitAsync(CancellationToken ct = default) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrainingDbContext).Assembly);
        modelBuilder.AddTransactionalOutbox();
    }
}
