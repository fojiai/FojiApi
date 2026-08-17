namespace FojiApi.Core.Interfaces.Services;

/// <summary>
/// Live metering for WhatsApp messages.
///
/// Meta bills per message, not per conversation, so this counts messages and
/// counts them as they happen. The nightly DailyStats aggregation cannot be the
/// basis for this: a limit enforced up to 24 hours late is a limit a customer
/// can blow straight through, and from 2026-10-01 every reply past it costs real
/// money.
/// </summary>
public interface IWhatsAppUsageService
{
    /// <summary>Usage for the company's current billing period.</summary>
    Task<WhatsAppUsageResult> GetUsageAsync(int companyId, CancellationToken ct = default);

    /// <summary>
    /// Records one outbound message and reports whether it was within the
    /// allowance. Call this immediately before sending: it is the gate as well
    /// as the meter.
    /// </summary>
    Task<WhatsAppConsumeResult> TryConsumeAsync(
        int companyId, WhatsAppMessageCategory category = WhatsAppMessageCategory.Service,
        CancellationToken ct = default);
}

public enum WhatsAppMessageCategory
{
    /// <summary>Free-form reply inside the 24h window.</summary>
    Service,
    Utility,
    Marketing,
}

/// <param name="Limit">Messages included in the plan. 0 means WhatsApp is not sold to this company.</param>
/// <param name="Unlimited">True when the plan grants an uncapped allowance.</param>
public record WhatsAppUsageResult(
    int Used, int Limit, bool Unlimited, DateOnly PeriodStart, DateOnly PeriodEnd,
    int OverageCentavos = 0)
{
    public int Remaining => Unlimited ? int.MaxValue : Math.Max(0, Limit - Used);
    public bool OverLimit => !Unlimited && Used >= Limit;

    /// <summary>Messages sent beyond the allowance, which are billable as overage.</summary>
    public int OverageMessages => Unlimited ? 0 : Math.Max(0, Used - Limit);

    /// <summary>What the overage is worth this period, in centavos.</summary>
    public int OverageOwedCentavos => OverageMessages * OverageCentavos;

    /// <summary>
    /// Whether a further send is permitted. A plan with no overage price stops
    /// at the limit — silence is recoverable, a surprise invoice is not.
    /// </summary>
    public bool CanSend => !OverLimit || OverageCentavos > 0;
}

/// <param name="Allowed">False when the send should not happen.</param>
public record WhatsAppConsumeResult(
    bool Allowed, int Used, int Limit, bool Unlimited, int OverageMessages = 0);
