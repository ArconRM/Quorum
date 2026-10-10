using Labeling.Domain.Labels;
using Labeling.Domain.Predictions;
using Labeling.Domain.Reviews;
using Labeling.Domain.Runs;
using Labeling.Domain.Teachers;
using Microsoft.EntityFrameworkCore;
using Quorum.Application.Abstractions;
using Quorum.Core.Persistence;

namespace Labeling.Infrastructure.Db;

/// <summary>База сервиса Labeling (БД labeling): учителя, прогоны, предсказания, метки, проверки + outbox/inbox.</summary>
public sealed class LabelingDbContext(DbContextOptions<LabelingDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<TeacherModel> Teachers => Set<TeacherModel>();
    public DbSet<LabelingRun> Runs => Set<LabelingRun>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<ConsensusLabel> Labels => Set<ConsensusLabel>();
    public DbSet<ReviewTask> Reviews => Set<ReviewTask>();

    public Task CommitAsync(CancellationToken ct = default) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LabelingDbContext).Assembly);
        modelBuilder.AddTransactionalOutbox();
    }
}
