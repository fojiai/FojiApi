using FojiApi.Core.Enums;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

/// <summary>
/// CRM KPIs — win rate, pipeline value, cycle length, sources. Distinct from
/// AnalyticsService, which only covers chat sessions and token usage.
/// </summary>
public class CrmAnalyticsService(FojiDbContext db) : ICrmAnalyticsService
{
    public async Task<CrmSummaryResult> GetSummaryAsync(int companyId, int days = 90)
    {
        if (days < 1) days = 1;
        if (days > 365) days = 365;
        var since = DateTime.UtcNow.AddDays(-days);
        var now = DateTime.UtcNow;

        // ── Open pipeline (current, not windowed) ────────────────────────────
        var openDeals = await db.Deals
            .Where(d => d.CompanyId == companyId && d.Status == DealStatus.Open)
            .Select(d => new { d.Value, d.Currency })
            .ToListAsync();

        // Deals carry their own currency; report the dominant one rather than
        // summing across currencies as if they were interchangeable.
        var currency = openDeals
            .GroupBy(d => d.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "BRL";

        // ── Closed deals in the window ───────────────────────────────────────
        var closed = await db.Deals
            .Where(d => d.CompanyId == companyId
                        && d.Status != DealStatus.Open
                        && d.ClosedAt != null
                        && d.ClosedAt >= since)
            .Select(d => new { d.Status, d.Value, d.CreatedAt, d.ClosedAt })
            .ToListAsync();

        var won = closed.Where(d => d.Status == DealStatus.Won).ToList();
        var lost = closed.Where(d => d.Status == DealStatus.Lost).ToList();
        var decided = won.Count + lost.Count;

        var cycleDays = won
            .Where(d => d.ClosedAt.HasValue)
            .Select(d => (d.ClosedAt!.Value - d.CreatedAt).TotalDays)
            .Where(x => x >= 0)
            .ToList();

        // ── Monthly won/lost trend ───────────────────────────────────────────
        var monthly = closed
            .Where(d => d.ClosedAt.HasValue)
            .GroupBy(d => new { d.ClosedAt!.Value.Year, d.ClosedAt!.Value.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyOutcomeResult(
                g.Key.Year,
                g.Key.Month,
                g.Count(x => x.Status == DealStatus.Won),
                g.Count(x => x.Status == DealStatus.Lost),
                g.Where(x => x.Status == DealStatus.Won).Sum(x => x.Value)))
            .ToList();

        // ── Funnel: open value per stage of the default pipeline ─────────────
        // PipelineStage has no Deals navigation, so aggregate separately and join.
        var stages = await db.PipelineStages
            .Where(s => s.CompanyId == companyId && s.Pipeline.IsDefault)
            .OrderBy(s => s.SortOrder)
            .Select(s => new { s.Id, s.Name, s.SortOrder })
            .ToListAsync();

        var openByStage = await db.Deals
            .Where(d => d.CompanyId == companyId && d.Status == DealStatus.Open)
            .GroupBy(d => d.StageId)
            .Select(g => new { StageId = g.Key, Count = g.Count(), Total = g.Sum(x => x.Value) })
            .ToDictionaryAsync(x => x.StageId, x => new { x.Count, x.Total });

        var funnel = stages
            .Select(s => openByStage.TryGetValue(s.Id, out var agg)
                ? new StageFunnelResult(s.Id, s.Name, s.SortOrder, agg.Count, agg.Total)
                : new StageFunnelResult(s.Id, s.Name, s.SortOrder, 0, 0m))
            .ToList();

        // ── Contacts & sources ───────────────────────────────────────────────
        var totalContacts = await db.Contacts.CountAsync(c => c.CompanyId == companyId);
        var newContacts = await db.Contacts.CountAsync(c => c.CompanyId == companyId && c.CreatedAt >= since);

        var sources = await db.Contacts
            .Where(c => c.CompanyId == companyId && c.CreatedAt >= since)
            .GroupBy(c => c.Source ?? "unknown")
            .Select(g => new SourceBreakdownResult(g.Key, g.Count()))
            .OrderByDescending(x => x.Contacts)
            .Take(8)
            .ToListAsync();

        // ── Tasks ────────────────────────────────────────────────────────────
        var openTasks = await db.CrmTasks
            .CountAsync(x => x.CompanyId == companyId && x.Status != CrmTaskStatus.Done);
        var overdueTasks = await db.CrmTasks
            .CountAsync(x => x.CompanyId == companyId
                             && x.Status != CrmTaskStatus.Done
                             && x.DueAt != null
                             && x.DueAt < now);

        return new CrmSummaryResult(
            OpenDeals: openDeals.Count,
            OpenValue: openDeals.Sum(d => d.Value),
            Currency: currency,
            WonDeals: won.Count,
            LostDeals: lost.Count,
            WonValue: won.Sum(d => d.Value),
            WinRate: decided == 0 ? 0 : Math.Round((double)won.Count / decided * 100, 1),
            AverageWonValue: won.Count == 0 ? 0m : Math.Round(won.Sum(d => d.Value) / won.Count, 2),
            AverageCycleDays: cycleDays.Count == 0 ? null : Math.Round(cycleDays.Average(), 1),
            TotalContacts: totalContacts,
            NewContacts: newContacts,
            OpenTasks: openTasks,
            OverdueTasks: overdueTasks,
            Funnel: funnel,
            MonthlyOutcomes: monthly,
            Sources: sources
        );
    }
}
