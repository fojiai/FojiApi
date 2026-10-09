namespace FojiApi.Core.Interfaces.Services;

/// <summary>
/// Subscriptions paid through Asaas.
///  - Monthly plans: card only, charged automatically (Asaas hosted checkout).
///  - Yearly plans: card, or Pix paid once for the year.
///  - Upgrades charge the prorated difference now and unlock when it's paid;
///    downgrades wait for the end of the paid period.
/// </summary>
public interface IBillingService
{
    Task<SubscriptionResult?> GetSubscriptionAsync(int companyId);

    Task<BillingProfileResult> GetProfileAsync(int companyId);
    Task<BillingProfileResult> UpdateProfileAsync(int companyId, UpdateBillingProfileRequest request);

    /// <summary>What choosing this plan would do, without doing it (for the confirm dialog).</summary>
    Task<PlanChangePreview> PreviewAsync(int companyId, ChoosePlanRequest request);

    /// <summary>Subscribe, upgrade, downgrade or switch cycle/method, whichever applies.</summary>
    Task<BillingActionResult> ChoosePlanAsync(int companyId, int userId, ChoosePlanRequest request, string? remoteIp);

    Task<BillingActionResult> CancelAsync(int companyId);
    Task<BillingActionResult> ResumeAsync(int companyId, int userId);
    Task<BillingActionResult> CancelPendingChangeAsync(int companyId);

    /// <summary>New card: a hosted checkout that replaces the current subscription (pays the overdue charge if there is one).</summary>
    Task<BillingActionResult> UpdateCardAsync(int companyId, int userId);

    Task<IReadOnlyList<BillingPaymentResult>> ListPaymentsAsync(int companyId);
    Task<BillingCheckoutResult?> GetCheckoutAsync(int companyId, int checkoutId);

    /// <summary>Stops charging at Asaas (company deleted, admin took over the plan). Local rows are left to the caller.</summary>
    Task StopAsaasSubscriptionsAsync(int companyId, CancellationToken ct = default);
}

public record SubscriptionResult(
    int Id,
    /// <summary>trialing | active | past_due | canceled | unpaid | incomplete</summary>
    string Status,
    SubscriptionPlanResult Plan,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    DateTime? TrialEndsAt,
    DateTime? CanceledAt,
    /// <summary>monthly | yearly</summary>
    string Cycle,
    /// <summary>credit_card | pix | manual | null (trial)</summary>
    string? PaymentMethod,
    decimal? Price,
    string? CardBrand,
    string? CardLast4,
    bool CancelAtPeriodEnd,
    /// <summary>A downgrade scheduled for CurrentPeriodEnd.</summary>
    SubscriptionPlanResult? PendingPlan,
    /// <summary>Paid through Asaas (not a trial or an admin-assigned plan).</summary>
    bool IsPaid,
    bool IsAdminAssigned,
    /// <summary>The invoice to pay when something is overdue or a Pix renewal is open.</summary>
    string? OpenInvoiceUrl,
    DateTime? PastDueSince,
    /// <summary>When features get locked if the overdue charge isn't paid.</summary>
    DateTime? SuspendsAt,
    /// <summary>A replacement subscription (new card, new cycle, resumed) starts on this date.</summary>
    DateTime? ReplacementStartsAt
);

public record SubscriptionPlanResult(int Id, string Name, int MaxAgents, bool HasWhatsApp, bool HasEscalationContacts, bool HasGoogleCalendar, bool HasCrm, int MaxConversationsPerMonth, int MaxMessagesPerMonth);

public record BillingProfileResult(string Name, string AccountType, string? CpfCnpj, bool Complete);

public record UpdateBillingProfileRequest(string Name, string AccountType, string CpfCnpj);

/// <param name="Cycle">monthly | yearly</param>
/// <param name="Method">credit_card | pix (pix only with yearly)</param>
public record ChoosePlanRequest(int PlanId, string Cycle, string Method);

/// <param name="Kind">new | upgrade | downgrade | switch | same</param>
/// <param name="AmountNow">Charged right away (new subscription, or the prorated upgrade difference).</param>
/// <param name="NewPrice">Recurring price from now on, per cycle.</param>
/// <param name="EffectiveAt">When the change takes effect (null = now / when paid).</param>
public record PlanChangePreview(string Kind, decimal AmountNow, decimal NewPrice, string Cycle, DateTime? EffectiveAt);

/// <param name="Action">redirect (send the browser to Url) | applied (done now) | scheduled (at EffectiveAt)</param>
public record BillingActionResult(string Action, string? Url = null, DateTime? EffectiveAt = null, int? CheckoutId = null);

public record BillingPaymentResult(
    int Id, string Kind, string Status, decimal Value, string? BillingType,
    DateOnly DueDate, DateTime? PaidAt, string? InvoiceUrl, string? NfseUrl, string? Description);

/// <param name="Status">pending | completed | expired | canceled</param>
public record BillingCheckoutResult(int Id, string Kind, string Status, string? Url);
