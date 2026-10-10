using Datasets.Domain.Cameras;
using Datasets.Domain.Frames;
using Datasets.Domain.Snapshots;
using Microsoft.EntityFrameworkCore;
using Quorum.Application.Abstractions;
using Quorum.Core.Persistence;

namespace Datasets.Infrastructure.Db;

/// <summary>База сервиса Datasets (БД datasets): камеры, кадры, снимки + outbox/inbox.</summary>
public sealed class DatasetsDbContext(DbContextOptions<DatasetsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<Frame> Frames => Set<Frame>();
    public DbSet<DatasetSnapshot> Snapshots => Set<DatasetSnapshot>();

    public Task CommitAsync(CancellationToken ct = default) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DatasetsDbContext).Assembly);
        modelBuilder.AddTransactionalOutbox();
    }
}
