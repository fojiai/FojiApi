using FojiApi.Infrastructure.Billing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.BackgroundJobs;

/// <summary>Applies stored Asaas webhooks a few seconds after they arrive.</summary>
public class BillingWebhookJob(IServiceScopeFactory scopeFactory, ILogger<BillingWebhookJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var webhooks = scope.ServiceProvider.GetRequiredService<BillingWebhookService>();
                // Drain the backlog, then wait for the next tick.
                while (await webhooks.ProcessPendingAsync(stoppingToken) > 0) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Asaas webhook processing loop failed");
            }
        }
    }
}

/// <summary>Runs the billing sweep (grace periods, period ends, overage) every hour.</summary>
public class BillingMaintenanceJob(IServiceScopeFactory scopeFactory, ILogger<BillingMaintenanceJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<BillingMaintenanceService>().RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Billing sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
