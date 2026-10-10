using Labeling.Application.Abstractions;
using Labeling.Infrastructure.Db;
using Labeling.Infrastructure.Grpc;
using Labeling.Infrastructure.Messaging;
using Labeling.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quorum.Core.Messaging;
using Quorum.Core.Persistence;
using Quorum.Grpc.Datasets;
using Quorum.Grpc.Worker;

namespace Labeling.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLabelingInfrastructure(this IServiceCollection services)
    {
        services.AddPostgres<LabelingDbContext>();
        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<IRunRepository, RunRepository>();
        services.AddScoped<IPredictionRepository, PredictionRepository>();
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<IReviewTaskRepository, ReviewTaskRepository>();

        services.AddGrpcClient<SnapshotQuery.SnapshotQueryClient>((sp, o) =>
            o.Address = new Uri(sp.GetRequiredService<IConfiguration>()["Grpc:Datasets"] ?? "http://localhost:5181"));
        services.AddGrpcClient<InferenceWorker.InferenceWorkerClient>((sp, o) =>
            o.Address = new Uri(sp.GetRequiredService<IConfiguration>()["Grpc:Worker"] ?? "http://localhost:50051"));
        services.AddScoped<IFrameSource, GrpcFrameSource>();
        services.AddScoped<ITeacherRunner, GrpcTeacherRunner>();

        services.AddMessaging<LabelingDbContext>(x =>
        {
            x.AddConsumer<StartLabelingConsumer>();
            x.AddConsumer<ProcessLabelingRunConsumer>((_, consumer) => consumer.ConcurrentMessageLimit = 1);
            x.AddConsumer<DiscardLabelingConsumer>();
        });
        return services;
    }
}
