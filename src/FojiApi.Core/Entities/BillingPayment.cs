using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

/// <summary>A charge at Asaas, mirrored from webhooks. Powers the invoice list in /billing.</summary>
public class BillingPayment : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? SubscriptionId { get; set; }
    public BillingPaymentKind Kind { get; set; }
    public BillingPaymentStatus Status { get; set; } = BillingPaymentStatus.Pending;

    /// <summary>pay_... Null only for waived charges that never reached Asaas.</summary>
    public string? AsaasPaymentId { get; set; }
    public decimal Value { get; set; }
    public string? BillingType { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? InvoiceUrl { get; set; }
    public string? NfseUrl { get; set; }
    public string? Description { get; set; }

    /// <summary>The usage window an overage charge covers (one charge per window).</summary>
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }

    /// <summary>We emailed the owner the payment link (Pix renewal, overage, unpaid upgrade).</summary>
    public DateTime? NotifiedCreatedAt { get; set; }

    /// <summary>We emailed the owner that this charge is overdue or the card was refused.</summary>
    public DateTime? NotifiedOverdueAt { get; set; }

    public Company Company { get; set; } = null!;
    public Subscription? Subscription { get; set; }
}
