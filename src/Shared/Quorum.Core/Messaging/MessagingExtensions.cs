using MassTransit;
using Quorum.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Quorum.Core.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// MassTransit + RabbitMQ + EF Core transactional outbox/inbox.
    /// Bus outbox — публикации из HTTP-запросов уходят в брокер только после коммита.
    /// Consumer outbox/inbox — каждое сообщение обрабатывается консьюмером ровно один раз.
    /// </summary>
    public static IServiceCollection AddMessaging<TDbContext>(
        this IServiceCollection services,
        Action<IBusRegistrationConfigurator>? configure = null)
        where TDbContext : DbContext
    {
        services.AddScoped<IIntegrationEventPublisher, MassTransitEventPublisher>();

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(1);
            });

            x.AddConfigureEndpointsCallback((context, _, cfg) =>
            {
                cfg.UseMessageRetry(r => r.Intervals(200, 1000, 3000));
                cfg.UseEntityFrameworkOutbox<TDbContext>(context);
            });

            configure?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                var config = context.GetRequiredService<IConfiguration>();
                var host = config["RabbitMq:Host"] ?? "localhost";
                var port = ushort.TryParse(config["RabbitMq:Port"], out var p) ? p : (ushort)5672;
                cfg.Host(host, port, "/", h =>
                {
                    h.Username(config["RabbitMq:Username"] ?? "guest");
                    h.Password(config["RabbitMq:Password"] ?? "guest");
                });
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
