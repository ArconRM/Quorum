using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quorum.Core.Messaging;
using Quorum.Core.Persistence;
using Quorum.Grpc.Labeling;
using Quorum.Grpc.Worker;
using Training.Application.Abstractions;
using Training.Infrastructure.Caching;
using Training.Infrastructure.Db;
using Training.Infrastructure.Grpc;
using Training.Infrastructure.Messaging;
using Training.Infrastructure.Repositories;
using Training.Infrastructure.Settings;

namespace Training.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTrainingInfrastructure(this IServiceCollection services)
    {
        services.AddPostgres<TrainingDbContext>();
        services.AddScoped<ICycleRepository, CycleRepository>();
        services.AddScoped<ITrainingJobRepository, TrainingJobRepository>();
        services.AddScoped<IModelVersionRepository, ModelVersionRepository>();
        services.AddScoped<IEvaluationRepository, EvaluationRepository>();
        services.AddSingleton<IAdaptationSettings, ConfigAdaptationSettings>();

        services.AddGrpcClient<TrainingSetQuery.TrainingSetQueryClient>((sp, o) =>
            o.Address = new Uri(sp.GetRequiredService<IConfiguration>()["Grpc:Labeling"] ?? "http://localhost:5281"));
        services.AddGrpcClient<InferenceWorker.InferenceWorkerClient>((sp, o) =>
            o.Address = new Uri(sp.GetRequiredService<IConfiguration>()["Grpc:Worker"] ?? "http://localhost:50051"));
        services.AddScoped<ILabeledDataSource, GrpcLabeledDataSource>();
        services.AddScoped<ITrainingWorker, GrpcTrainingWorker>();

        // Redis как L2 для HybridCache
        services.AddStackExchangeRedisCache(_ => { });
        services.AddOptions<RedisCacheOptions>().Configure<IConfiguration>((o, config) =>
        {
            o.Configuration = config.GetConnectionString("Redis");
            o.InstanceName = "training:";
        });
        services.AddHybridCache();
        services.AddScoped<IPublishedModelCache, PublishedModelCache>();

        services.AddMessaging<TrainingDbContext>(x =>
        {
            x.AddConsumer<AdaptationRequestedConsumer>();
            x.AddConsumer<SnapshotCreatedConsumer>();
            x.AddConsumer<SnapshotRejectedConsumer>();
            x.AddConsumer<LabelingCompletedConsumer>();
            x.AddConsumer<LabelingFailedConsumer>();
            x.AddConsumer<RunTrainingJobConsumer>((_, consumer) => consumer.ConcurrentMessageLimit = 1);
            x.AddConsumer<EvaluateCandidateConsumer>();
        });
        return services;
    }
}
