using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

public class Subscription : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int PlanId { get; set; }
    public SubscriptionStatus Status { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? StripeCustomerId { get; set; }
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? CanceledAt { get; set; }

    /// <summary>
    /// When set, this subscription was manually assigned by a super-admin rather than
    /// going through Stripe checkout. The value is the admin's UserId.
    /// </summary>
    public int? AssignedByAdminId { get; set; }

    /// <summary>Optional admin note explaining the custom arrangement (e.g. "Invoiced monthly via PIX").</summary>
    public string? AdminNotes { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public Plan Plan { get; set; } = null!;
    public User? AssignedByAdmin { get; set; }
}
