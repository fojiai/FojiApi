using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

public class Subscription : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int PlanId { get; set; }
    public SubscriptionStatus Status { get; set; }

    public BillingCycle Cycle { get; set; } = BillingCycle.Monthly;
    public BillingPaymentMethod? PaymentMethod { get; set; }

    /// <summary>The Asaas subscription (sub_...). Null for trials and admin-assigned plans.</summary>
    public string? AsaasSubscriptionId { get; set; }

    /// <summary>
    /// Asaas card token from the last card payment. Lets us charge upgrades and
    /// WhatsApp overage without asking for the card again (needs tokenization
    /// enabled on the Asaas account).
    /// </summary>
    public string? CardToken { get; set; }
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }

    /// <summary>Price charged per cycle, as agreed at checkout (BRL).</summary>
    public decimal? Price { get; set; }

    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? CanceledAt { get; set; }

    /// <summary>Canceled by the customer: access continues until CurrentPeriodEnd, nothing more is charged.</summary>
    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>When the oldest unpaid charge became overdue (drives the grace period).</summary>
    public DateTime? PastDueSince { get; set; }

    /// <summary>A downgrade waiting for the end of the paid period.</summary>
    public int? PendingPlanId { get; set; }

    /// <summary>
    /// When set, this subscription was manually assigned by a super-admin rather than
    /// going through checkout. The value is the admin's UserId.
    /// </summary>
    public int? AssignedByAdminId { get; set; }

    /// <summary>Optional admin note explaining the custom arrangement (e.g. "Invoiced monthly via PIX").</summary>
    public string? AdminNotes { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public Plan Plan { get; set; } = null!;
    public Plan? PendingPlan { get; set; }
    public User? AssignedByAdmin { get; set; }
}
