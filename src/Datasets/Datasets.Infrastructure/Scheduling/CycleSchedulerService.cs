using Datasets.Application.Features.Cameras.RequestDueCycles;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Datasets.Infrastructure.Scheduling;

/// <summary>
/// Периодическая адаптация: раз в Scheduler:IntervalSeconds проверяет камеры и запрашивает циклы.
/// Дубликаты (несколько реплик, повторный тик) безопасны — Training не запускает второй цикл на камере.
/// </summary>
internal sealed class CycleSchedulerService(IServiceScopeFactory scopes, IConfiguration config, ILogger<CycleSchedulerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Scheduler:Enabled", true)) return;
        var interval = TimeSpan.FromSeconds(config.GetValue("Scheduler:IntervalSeconds", 60));

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var requested = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new RequestDueCyclesCommand(), stoppingToken);
                if (requested > 0) logger.LogInformation("Requested {Count} adaptation cycle(s)", requested);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Scheduler tick failed");
            }
        }
    }
}
