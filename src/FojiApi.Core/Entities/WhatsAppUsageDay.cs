namespace FojiApi.Core.Entities;

/// <summary>
/// One row per company per day, counting the WhatsApp messages we actually pay
/// Meta for.
///
/// This exists as its own table rather than being derived from WhatsAppMessages
/// because that table only holds the shared-inbox threads — an agent in Agent
/// mode replies straight from the worker and is recorded nowhere. Those AI
/// replies are the bulk of what we get billed for, so the meter has to be fed
/// explicitly by every send path.
///
/// Categories are split because they are an order of magnitude apart: a service
/// reply costs ~R$0.035 in Brazil, a marketing template ~R$0.32.
/// </summary>
public class WhatsAppUsageDay
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    /// <summary>UTC day. Billing periods are summed from these.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Free-form replies inside the 24h window. Free until 2026-10-01.</summary>
    public int ServiceMessages { get; set; }

    /// <summary>Templates that cost the utility rate.</summary>
    public int UtilityMessages { get; set; }

    /// <summary>Campaign templates — roughly 9x the utility rate in Brazil.</summary>
    public int MarketingMessages { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
