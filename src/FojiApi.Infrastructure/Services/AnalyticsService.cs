using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class AnalyticsService(FojiDbContext db) : IAnalyticsService
{
    public async Task<CompanyStatsResult> GetCompanyStatsAsync(int companyId, DateOnly? from, DateOnly? to)
    {
        var effectiveTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveFrom = from ?? effectiveTo.AddDays(-29); // last 30 days inclusive

        var stats = await db.DailyStats
            .Where(s => s.CompanyId == companyId
                        && s.StatDate >= effectiveFrom
                        && s.StatDate <= effectiveTo)
            .OrderBy(s => s.StatDate)
            .Select(s => new DailyStatResult(s.StatDate, s.Sessions, s.Messages, s.InputTokens, s.OutputTokens))
            .ToListAsync();

        return new CompanyStatsResult(
            CompanyId: companyId,
            TotalSessions: stats.Sum(s => s.Sessions),
            TotalMessages: stats.Sum(s => s.Messages),
            TotalInputTokens: stats.Sum(s => s.InputTokens),
            TotalOutputTokens: stats.Sum(s => s.OutputTokens),
            DailyStats: stats
        );
    }
}
