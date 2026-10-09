using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

/// <summary>
/// Something the customer was sent to pay: a hosted Asaas checkout (card
/// subscription), a Pix subscription's first invoice, or a one-off upgrade charge.
/// Its id travels to Asaas as externalReference ("foji:chk:{Id}") so webhooks
/// find their way back.
/// </summary>
public class BillingCheckout : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public BillingCheckoutKind Kind { get; set; }
    public BillingCheckoutStatus Status { get; set; } = BillingCheckoutStatus.Pending;
    public int PlanId { get; set; }
    public BillingCycle Cycle { get; set; }
    public BillingPaymentMethod Method { get; set; }
    public decimal Amount { get; set; }

    /// <summary>For replacements: the subscription to retire once this one is paid.</summary>
    public int? ReplacesSubscriptionId { get; set; }

    /// <summary>For upgrades: the subscription being upgraded.</summary>
    public int? SubscriptionId { get; set; }

    /// <summary>First due date of the new subscription (Brasília date).</summary>
    public DateOnly? StartDate { get; set; }

    public string? AsaasCheckoutId { get; set; }
    public string? AsaasSubscriptionId { get; set; }
    public string? AsaasPaymentId { get; set; }
    public string? Url { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Company Company { get; set; } = null!;
    public Plan Plan { get; set; } = null!;
}
