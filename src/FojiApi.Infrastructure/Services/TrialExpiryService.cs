using FojiApi.Core.Enums;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Services;

/// <summary>
/// Runs daily to:
///  1. Send trial-ending reminder emails (at 3 days and 1 day before expiry).
///  2. Flip subscription status from Trialing → Canceled when the trial has ended.
/// </summary>
public class TrialExpiryService(
    FojiDbContext db,
    IEmailService emailService,
    ILogger<TrialExpiryService> logger
) : ITrialExpiryService
{
    private static readonly int[] ReminderDays = [3, 1];

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await SendReminderEmailsAsync(cancellationToken);
        await ExpireTrialsAsync(cancellationToken);
    }

    // ── Reminder emails ──────────────────────────────────────────────────────

    private async Task SendReminderEmailsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        foreach (var days in ReminderDays)
        {
            var windowStart = now.AddDays(days).Date;
            var windowEnd   = windowStart.AddDays(1);

            var trials = await db.Subscriptions
                .Include(s => s.Company)
                .Where(s =>
                    s.Status == SubscriptionStatus.Trialing &&
                    s.TrialEndsAt.HasValue &&
                    s.TrialEndsAt.Value >= windowStart &&
                    s.TrialEndsAt.Value < windowEnd)
                .ToListAsync(ct);

            foreach (var sub in trials)
            {
                var owner = await db.UserCompanies
                    .Include(uc => uc.User)
                    .Where(uc =>
                        uc.CompanyId == sub.CompanyId &&
                        uc.Role == CompanyRole.Owner &&
                        uc.IsActive)
                    .Select(uc => uc.User)
                    .FirstOrDefaultAsync(ct);

                if (owner == null) continue;

                try
                {
                    await emailService.SendTrialEndingAsync(
                        owner.Email, owner.FirstName, sub.Company.Name, days);

                    logger.LogInformation(
                        "Trial-ending reminder sent: companyId={CompanyId} daysLeft={Days}",
                        sub.CompanyId, days);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to send trial-ending reminder: companyId={CompanyId} daysLeft={Days}",
                        sub.CompanyId, days);
                }
            }
        }
    }

    // ── Expire ended trials ──────────────────────────────────────────────────

    private async Task ExpireTrialsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var expired = await db.Subscriptions
            .Include(s => s.Company)
            .Where(s =>
                s.Status == SubscriptionStatus.Trialing &&
                s.TrialEndsAt.HasValue &&
                s.TrialEndsAt.Value < now)
            .ToListAsync(ct);

        if (expired.Count == 0) return;

        foreach (var sub in expired)
        {
            sub.Status = SubscriptionStatus.Canceled;
            sub.CanceledAt = now;
            logger.LogInformation(
                "Trial expired and canceled: companyId={CompanyId} subscriptionId={Id}",
                sub.CompanyId, sub.Id);

            // Notify the owner
            var owner = await db.UserCompanies
                .Include(uc => uc.User)
                .Where(uc =>
                    uc.CompanyId == sub.CompanyId &&
                    uc.Role == CompanyRole.Owner &&
                    uc.IsActive)
                .Select(uc => uc.User)
                .FirstOrDefaultAsync(ct);

            if (owner != null)
            {
                try
                {
                    await emailService.SendSubscriptionCancelledAsync(
                        owner.Email, owner.FirstName, sub.Company.Name);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to send cancellation email after trial expiry: companyId={CompanyId}",
                        sub.CompanyId);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
