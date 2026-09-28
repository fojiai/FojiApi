using FojiApi.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.BackgroundJobs;

/// <summary>
/// Quietly resolves shared-inbox conversations that have gone idle, so the inbox
/// shows what still needs someone instead of every thread that ever happened.
///
/// Nothing is sent to the customer — a conversation ending is invisible to them,
/// and the next message they send reopens it. Conversations where the customer
/// spoke last are never touched (see IWhatsAppInboxService.AutoResolveIdleAsync).
/// </summary>
public class WhatsAppInboxAutoResolveJob(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<WhatsAppInboxAutoResolveJob> logger) : BackgroundService
{
    /// <summary>
    /// Idle time before resolving. Defaults to Meta's 24h service window: past it
    /// the team can't send a free-form reply anyway, so the thread is effectively over.
    /// </summary>
    private TimeSpan IdleFor => TimeSpan.FromHours(
        configuration.GetValue("WhatsAppInbox:AutoResolveAfterHours", 24));

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(3);

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
                var inbox = scope.ServiceProvider.GetRequiredService<IWhatsAppInboxService>();

                // A single idempotent UPDATE — safe if several instances run it at once.
                var resolved = await inbox.AutoResolveIdleAsync(IdleFor, stoppingToken);
                if (resolved > 0)
                    logger.LogInformation("Inbox sweep: auto-resolved {Count} idle conversation(s)", resolved);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never let one bad sweep kill the loop; the next pass is 30 minutes away.
                logger.LogError(ex, "Inbox auto-resolve sweep failed");
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
