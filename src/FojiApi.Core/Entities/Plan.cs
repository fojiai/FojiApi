namespace FojiApi.Core.Entities;

public class Plan : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyPriceUsd { get; set; }
    public string? StripePriceId { get; set; }
    public int MaxAgents { get; set; }
    public bool HasWhatsApp { get; set; } = false;
    public bool HasEscalationContacts { get; set; } = false;

    /// <summary>Maximum new chat sessions per company per month. 0 = unlimited.</summary>
    public int MaxConversationsPerMonth { get; set; } = 0;

    /// <summary>Maximum total messages (user + AI) per company per month. 0 = unlimited.</summary>
    public int MaxMessagesPerMonth { get; set; } = 0;
    public int TrialDays { get; set; } = 7;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When false, this plan is hidden from the public pricing page and can only be
    /// assigned by a super-admin. Use for custom/negotiated plans.
    /// </summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>
    /// If set, this custom plan is exclusively for this one company.
    /// Prevents it from being accidentally assigned to others.
    /// </summary>
    public int? CustomForCompanyId { get; set; }

    // Navigation
    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public Company? CustomForCompany { get; set; }
}
