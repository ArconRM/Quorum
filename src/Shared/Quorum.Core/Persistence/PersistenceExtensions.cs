using MassTransit;
using Quorum.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Quorum.Core.Persistence;

public static class PersistenceExtensions
{
    /// <summary>DbContext на PostgreSQL (ConnectionStrings:Postgres) + IUnitOfWork.</summary>
    public static IServiceCollection AddPostgres<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext, IUnitOfWork
    {
        // Строка подключения читается лениво — её можно подменить в интеграционных тестах.
        services.AddDbContext<TDbContext>((sp, o) =>
            o.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TDbContext>());
        return services;
    }

    /// <summary>Таблицы MassTransit для transactional outbox / inbox.</summary>
    public static void AddTransactionalOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    /// <summary>
    /// Применяет миграции из Db/Migrations. Пока миграции не сгенерированы
    /// (./scripts/add-migration.sh Initial) — создаёт схему по модели через EnsureCreated.
    /// С ретраями, пока Postgres поднимается.
    /// </summary>
    public static async Task MigrateDatabaseAsync<TDbContext>(this IServiceProvider services) where TDbContext : DbContext
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
                if (db.Database.GetMigrations().Any())
                {
                    await db.Database.MigrateAsync();
                }
                else
                {
                    logger.LogWarning("No EF migrations found for {Context}, falling back to EnsureCreated", typeof(TDbContext).Name);
                    await db.Database.EnsureCreatedAsync();
                }
                return;
            }
            catch (Exception ex) when (attempt < 15)
            {
                logger.LogWarning("Database is not ready yet (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }
}
