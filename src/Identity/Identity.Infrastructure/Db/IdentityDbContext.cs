using Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Quorum.Application.Abstractions;
using Quorum.Core.Persistence;

namespace Identity.Infrastructure.Db;

/// <summary>База сервиса Identity (БД identity): пользователи + outbox/inbox.</summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();

    public Task CommitAsync(CancellationToken ct = default) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.AddTransactionalOutbox();
    }
}
