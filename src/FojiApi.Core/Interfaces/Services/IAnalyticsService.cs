namespace FojiApi.Core.Interfaces.Services;

public record DailyStatResult(
    DateOnly StatDate,
    int Sessions,
    int Messages,
    long InputTokens,
    long OutputTokens
);

public record CompanyStatsResult(
    int CompanyId,
    int TotalSessions,
    int TotalMessages,
    long TotalInputTokens,
    long TotalOutputTokens,
    IEnumerable<DailyStatResult> DailyStats
);

public interface IAnalyticsService
{
    /// <summary>
    /// Returns daily stats for a company within a date range.
    /// Defaults to the last 30 days when from/to are null.
    /// </summary>
    Task<CompanyStatsResult> GetCompanyStatsAsync(int companyId, DateOnly? from, DateOnly? to);
}
