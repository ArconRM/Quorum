using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Datasets.IntegrationTests.Infrastructure;

/// <summary>Настоящий Datasets.Service (in-process) поверх Postgres и RabbitMQ в контейнерах, кадры — во временной папке.</summary>
public sealed class DatasetsApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder()
        .WithImage("rabbitmq:4-management-alpine").WithUsername("test").WithPassword("test").Build();

    public string FramesRoot { get; } = Path.Combine(Path.GetTempPath(), "quorum-frames-" + Guid.NewGuid().ToString("N"));
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("RabbitMq:Host", _rabbit.Hostname);
            builder.UseSetting("RabbitMq:Port", _rabbit.GetMappedPublicPort(5672).ToString());
            builder.UseSetting("RabbitMq:Username", "test");
            builder.UseSetting("RabbitMq:Password", "test");
            builder.UseSetting("Storage:FramesRoot", FramesRoot);
            builder.UseSetting("Scheduler:Enabled", "false");
        });
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
        if (Directory.Exists(FramesRoot)) Directory.Delete(FramesRoot, recursive: true);
    }
}
