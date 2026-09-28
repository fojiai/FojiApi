using FojiApi.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.BackgroundJobs;

/// <summary>
/// Hybrid-mode safety net. When the AI calls the team, it goes quiet in that
/// conversation — so if nobody picks it up (lunch, night, a busy afternoon) the
/// customer would be left talking to no one. After the timeout, the customer is
/// told plainly that the team will reply here, and the AI resumes; the inbox
/// keeps flagging that they want a person.
/// </summary>
public class WhatsAppEscalationSweepJob(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<WhatsAppEscalationSweepJob> logger) : BackgroundService
{
    private TimeSpan Timeout => TimeSpan.FromMinutes(
        Math.Max(1, configuration.GetValue("WhatsAppInbox:EscalationTimeoutMinutes", 10)));

    // Every minute: the timeout is short, and a customer waiting on a person
    // shouldn't wait an extra half hour for the sweep to notice.
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
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
                var inbox = scope.ServiceProvider.GetRequiredService<IWhatsAppInboxService>();

                var handled = await inbox.HandleUnansweredEscalationsAsync(Timeout, stoppingToken);
                if (handled > 0)
                    logger.LogInformation(
                        "Escalation sweep: {Count} request(s) for a person went unanswered — customer told, AI resumed",
                        handled);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Escalation sweep failed");
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
