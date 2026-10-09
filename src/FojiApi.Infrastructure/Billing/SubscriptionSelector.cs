using FojiApi.Core.Entities;
using FojiApi.Core.Enums;

namespace FojiApi.Infrastructure.Billing;

/// <summary>One rule, used everywhere, for "which subscription is this company on".</summary>
public static class SubscriptionSelector
{
    /// <summary>
    /// Statuses that get the plan's features. PastDue is served during the grace
    /// period (the same rule foji-ai-api applies); Unpaid and Incomplete are not.
    /// </summary>
    ///
    /// Typed as IReadOnlyList, not an array: with C# 14 an array's .Contains binds to
    /// MemoryExtensions.Contains(ReadOnlySpan), which EF Core can't translate to SQL.
    public static readonly IReadOnlyList<SubscriptionStatus> Serving =
        [SubscriptionStatus.Active, SubscriptionStatus.Trialing, SubscriptionStatus.PastDue];

    /// <summary>Statuses that still bill at Asaas and need a decision from the customer.</summary>
    public static readonly IReadOnlyList<SubscriptionStatus> Live =
        [SubscriptionStatus.Active, SubscriptionStatus.Trialing, SubscriptionStatus.PastDue, SubscriptionStatus.Unpaid];

    /// <summary>
    /// The newest live subscription, else the newest of any kind (so a canceled
    /// plan still shows on the billing page). Incomplete rows never win: they are
    /// checkouts nobody has paid yet.
    /// </summary>
    public static Subscription? PickCurrent(IEnumerable<Subscription> subs)
    {
        var list = subs.Where(s => s.Status != SubscriptionStatus.Incomplete)
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id).ToList();
        return list.FirstOrDefault(s => Live.Contains(s.Status)) ?? list.FirstOrDefault();
    }

    public static string StatusName(SubscriptionStatus status) => status switch
    {
        SubscriptionStatus.PastDue => "past_due",
        _ => status.ToString().ToLowerInvariant(),
    };

    public static string? MethodName(BillingPaymentMethod? method) => method switch
    {
        BillingPaymentMethod.CreditCard => "credit_card",
        BillingPaymentMethod.Pix => "pix",
        BillingPaymentMethod.Manual => "manual",
        _ => null,
    };
}
