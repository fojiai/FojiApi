namespace FojiApi.Core.Interfaces.Services;

/// <summary>Open pipeline value and deal count for one stage.</summary>
public record StageFunnelResult(int StageId, string StageName, int SortOrder, int OpenDeals, decimal OpenValue);

/// <summary>Won/lost counts and value for a calendar month.</summary>
public record MonthlyOutcomeResult(int Year, int Month, int Won, int Lost, decimal WonValue);

/// <summary>Where contacts came from.</summary>
public record SourceBreakdownResult(string Source, int Contacts);

public record CrmSummaryResult(
    // Pipeline (open deals only)
    int OpenDeals,
    decimal OpenValue,
    string Currency,

    // Outcomes over the requested window
    int WonDeals,
    int LostDeals,
    decimal WonValue,
    /// <summary>Won / (Won + Lost) as a percentage, 0 when nothing has closed.</summary>
    double WinRate,
    /// <summary>Average value of won deals, 0 when none.</summary>
    decimal AverageWonValue,
    /// <summary>Mean days from deal creation to close, null when nothing has closed.</summary>
    double? AverageCycleDays,

    // Activity
    int TotalContacts,
    int NewContacts,
    int OpenTasks,
    int OverdueTasks,

    IEnumerable<StageFunnelResult> Funnel,
    IEnumerable<MonthlyOutcomeResult> MonthlyOutcomes,
    IEnumerable<SourceBreakdownResult> Sources
);

public interface ICrmAnalyticsService
{
    /// <summary>
    /// CRM KPIs for a company. <paramref name="days"/> bounds the outcome and
    /// new-contact windows; open pipeline is always current.
    /// </summary>
    Task<CrmSummaryResult> GetSummaryAsync(int companyId, int days = 90);
}
