using FojiApi.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.HostedServices;

/// <summary>
/// Background hosted service that runs <see cref="ITrialExpiryService.RunAsync"/> once per day,
/// shortly after midnight UTC. Uses a scoped service resolved per run to avoid DbContext issues.
/// </summary>
public class TrialExpiryHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<TrialExpiryHostedService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TrialExpiryHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Sleep until next midnight UTC
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1).AddMinutes(5); // 00:05 UTC daily
            var delay = nextRun - now;

            logger.LogInformation("TrialExpiryHostedService next run at {NextRun} UTC (in {Delay:hh\\:mm})",
                nextRun, delay);

            await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            await RunJobAsync(stoppingToken);
        }
    }

    private async Task RunJobAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ITrialExpiryService>();
            await svc.RunAsync(ct);
            logger.LogInformation("TrialExpiryHostedService completed successfully.");
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested — expected
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TrialExpiryHostedService encountered an error.");
        }
    }
}
