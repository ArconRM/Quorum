using Datasets.Application.Abstractions;
using Datasets.Infrastructure.Db;
using Datasets.Infrastructure.Messaging;
using Datasets.Infrastructure.Repositories;
using Datasets.Infrastructure.Scheduling;
using Datasets.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Quorum.Core.Messaging;
using Quorum.Core.Persistence;

namespace Datasets.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDatasetsInfrastructure(this IServiceCollection services)
    {
        services.AddPostgres<DatasetsDbContext>();
        services.AddScoped<ICameraRepository, CameraRepository>();
        services.AddScoped<IFrameRepository, FrameRepository>();
        services.AddScoped<ISnapshotRepository, SnapshotRepository>();
        services.AddSingleton<IFrameStorage, LocalFrameStorage>();
        services.AddHostedService<CycleSchedulerService>();

        services.AddMessaging<DatasetsDbContext>(x =>
        {
            x.AddConsumer<CreateSnapshotConsumer>();
            x.AddConsumer<ReleaseSnapshotConsumer>();
        });
        return services;
    }
}
