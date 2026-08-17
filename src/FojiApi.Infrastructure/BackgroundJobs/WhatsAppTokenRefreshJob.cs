using FojiApi.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.BackgroundJobs;

/// <summary>
/// Keeps customers' WhatsApp connections alive.
///
/// Embedded Signup issues 60-day tokens. Nobody is going to reconnect their
/// WhatsApp every two months, and a connection that dies quietly looks exactly
/// like "no one messaged us today" — so this refreshes well ahead of expiry and
/// flags anything it cannot save.
/// </summary>
public class WhatsAppTokenRefreshJob(
    IServiceScopeFactory scopeFactory,
    ILogger<WhatsAppTokenRefreshJob> logger) : BackgroundService
{
    /// <summary>
    /// Twice a day is plenty against a 15-day refresh window, and it means a
    /// deploy or a brief outage cannot cause a missed renewal.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    /// <summary>Let the app finish starting before doing outbound work.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var onboarding = scope.ServiceProvider.GetRequiredService<IWhatsAppOnboardingService>();

                var (refreshed, failed) = await onboarding.RefreshExpiringAsync(stoppingToken);

                if (refreshed > 0 || failed > 0)
                {
                    logger.LogInformation(
                        "WhatsApp token sweep: {Refreshed} refreshed, {Failed} now need the customer to reconnect",
                        refreshed, failed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never let one bad sweep kill the loop — the next pass is only
                // twelve hours away and the tokens are still valid for days.
                logger.LogError(ex, "WhatsApp token refresh sweep failed");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
