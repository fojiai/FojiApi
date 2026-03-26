namespace FojiApi.Core.Entities;

/// <summary>
/// Nightly aggregated analytics per company, populated by the analytics Lambda.
/// </summary>
public class DailyStat : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>The UTC calendar date this stat covers.</summary>
    public DateOnly StatDate { get; set; }

    public int Sessions { get; set; }
    public int Messages { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
}
