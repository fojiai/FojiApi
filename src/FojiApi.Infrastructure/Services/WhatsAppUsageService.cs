using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Services;

/// <summary>See IWhatsAppUsageService. Counts what Meta bills us for, as it happens.</summary>
public class WhatsAppUsageService(
    FojiDbContext db,
    ILogger<WhatsAppUsageService> logger) : IWhatsAppUsageService
{
    public async Task<WhatsAppUsageResult> GetUsageAsync(int companyId, CancellationToken ct = default)
    {
        var (start, end) = await GetBillingPeriodAsync(companyId, ct);
        var (limit, unlimited, overageCentavos) = await GetAllowanceAsync(companyId, ct);

        var used = await db.WhatsAppUsageDays
            .Where(u => u.CompanyId == companyId && u.Date >= start && u.Date <= end)
            .SumAsync(u => u.ServiceMessages + u.UtilityMessages + u.MarketingMessages, ct);

        return new WhatsAppUsageResult(used, limit, unlimited, start, end, overageCentavos);
    }

    public async Task<WhatsAppConsumeResult> TryConsumeAsync(
        int companyId,
        WhatsAppMessageCategory category = WhatsAppMessageCategory.Service,
        CancellationToken ct = default)
    {
        // Marketing is refused before anything else. It is ~9x the cost of a
        // utility message, it is never free inside the 24h window, and a single
        // campaign can dwarf a month of ordinary replies on the bill.
        if (category == WhatsAppMessageCategory.Marketing && !await MarketingAllowedAsync(companyId, ct))
        {
            logger.LogWarning(
                "Blocked a marketing template for company {CompanyId} — not enabled on this plan",
                companyId);
            var blocked = await GetUsageAsync(companyId, ct);
            return new WhatsAppConsumeResult(false, blocked.Used, blocked.Limit, blocked.Unlimited, blocked.OverageMessages);
        }

        var usage = await GetUsageAsync(companyId, ct);

        if (!usage.CanSend)
        {
            logger.LogWarning(
                "WhatsApp allowance exhausted for company {CompanyId}: {Used}/{Limit}, no overage price set",
                companyId, usage.Used, usage.Limit);
            return new WhatsAppConsumeResult(false, usage.Used, usage.Limit, usage.Unlimited, usage.OverageMessages);
        }

        if (usage.OverLimit)
        {
            // Past the allowance but the plan prices overage, so the agent keeps
            // answering and the extra messages are billed rather than dropped.
            logger.LogInformation(
                "WhatsApp overage for company {CompanyId}: {Used}/{Limit} at {Centavos} centavos/message",
                companyId, usage.Used, usage.Limit, usage.OverageCentavos);
        }

        await IncrementAsync(companyId, category, ct);
        var after = usage with { Used = usage.Used + 1 };
        return new WhatsAppConsumeResult(true, after.Used, after.Limit, after.Unlimited, after.OverageMessages);
    }

    /// <summary>
    /// Upsert-and-increment in one statement, so concurrent sends cannot lose a
    /// count to a read-modify-write race.
    ///
    /// The check in TryConsumeAsync is deliberately not locked: two sends racing
    /// at the exact boundary can put a company one or two messages over its
    /// allowance. On a monthly allowance in the hundreds that is worth far less
    /// than holding a lock on the hot path of every single reply.
    /// </summary>
    private async Task IncrementAsync(int companyId, WhatsAppMessageCategory category, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // One constant statement per category rather than an interpolated column
        // name: verbose, but there is no way for a column name to be assembled
        // at runtime on a path that writes to the database.
        var sql = category switch
        {
            WhatsAppMessageCategory.Marketing => """
                INSERT INTO "WhatsAppUsageDays" ("CompanyId","Date","ServiceMessages","UtilityMessages","MarketingMessages","UpdatedAt")
                VALUES ({0}, {1}, 0, 0, 1, NOW() AT TIME ZONE 'utc')
                ON CONFLICT ("CompanyId","Date") DO UPDATE
                SET "MarketingMessages" = "WhatsAppUsageDays"."MarketingMessages" + 1,
                    "UpdatedAt" = NOW() AT TIME ZONE 'utc'
                """,
            WhatsAppMessageCategory.Utility => """
                INSERT INTO "WhatsAppUsageDays" ("CompanyId","Date","ServiceMessages","UtilityMessages","MarketingMessages","UpdatedAt")
                VALUES ({0}, {1}, 0, 1, 0, NOW() AT TIME ZONE 'utc')
                ON CONFLICT ("CompanyId","Date") DO UPDATE
                SET "UtilityMessages" = "WhatsAppUsageDays"."UtilityMessages" + 1,
                    "UpdatedAt" = NOW() AT TIME ZONE 'utc'
                """,
            _ => """
                INSERT INTO "WhatsAppUsageDays" ("CompanyId","Date","ServiceMessages","UtilityMessages","MarketingMessages","UpdatedAt")
                VALUES ({0}, {1}, 1, 0, 0, NOW() AT TIME ZONE 'utc')
                ON CONFLICT ("CompanyId","Date") DO UPDATE
                SET "ServiceMessages" = "WhatsAppUsageDays"."ServiceMessages" + 1,
                    "UpdatedAt" = NOW() AT TIME ZONE 'utc'
                """,
        };

        await db.Database.ExecuteSqlRawAsync(sql, [companyId, today], ct);
    }

    /// <summary>
    /// The plan's WhatsApp allowance. Zero means the company cannot send at all,
    /// which is the correct default for a plan that does not include WhatsApp.
    /// </summary>
    private async Task<(int Limit, bool Unlimited, int OverageCentavos)> GetAllowanceAsync(
        int companyId, CancellationToken ct)
    {
        var plan = await db.Subscriptions
            .Where(s => s.CompanyId == companyId
                        && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.Plan.HasWhatsApp, s.Plan.WhatsAppMessagesPerMonth, s.Plan.WhatsAppOverageCentavos })
            .FirstOrDefaultAsync(ct);

        // No WhatsApp on the plan means no sending, and no overage price can
        // change that — they have not bought the channel.
        if (plan is null || !plan.HasWhatsApp) return (0, false, 0);

        // -1 is the explicit "uncapped" marker; 0 means the plan sells WhatsApp
        // but grants no messages, which would be a misconfiguration.
        return plan.WhatsAppMessagesPerMonth < 0
            ? (0, true, 0)
            : (plan.WhatsAppMessagesPerMonth, false, plan.WhatsAppOverageCentavos);
    }

    private async Task<bool> MarketingAllowedAsync(int companyId, CancellationToken ct)
        => await db.Subscriptions
            .Where(s => s.CompanyId == companyId
                        && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.Plan.WhatsAppAllowMarketing)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Align the meter with what the customer is billed for. Falls back to the
    /// calendar month when there is no Stripe period — otherwise a mid-month
    /// upgrade would silently reset someone's allowance.
    /// </summary>
    private async Task<(DateOnly Start, DateOnly End)> GetBillingPeriodAsync(int companyId, CancellationToken ct)
    {
        var sub = await db.Subscriptions
            .Where(s => s.CompanyId == companyId
                        && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.CurrentPeriodStart, s.CurrentPeriodEnd })
            .FirstOrDefaultAsync(ct);

        var now = DateTime.UtcNow;
        if (sub?.CurrentPeriodStart is { } start && sub.CurrentPeriodEnd is { } end && end > now)
            return (DateOnly.FromDateTime(start), DateOnly.FromDateTime(end));

        var monthStart = new DateOnly(now.Year, now.Month, 1);
        return (monthStart, monthStart.AddMonths(1).AddDays(-1));
    }
}
