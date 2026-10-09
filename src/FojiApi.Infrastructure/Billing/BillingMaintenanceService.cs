using FojiApi.Core.Billing;
using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Asaas;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Billing;

/// <summary>
/// The hourly billing sweep. Asaas never cancels an unpaid subscription on its own
/// and has no proration or usage billing, so these decisions live here:
///  1. Paid periods that ended after a cancel or a replacement.
///  2. Scheduled downgrades whose period ended without a renewal applying them.
///  3. Overdue past the grace period: lock features (Unpaid).
///  4. Unpaid for too long: stop charging at Asaas and cancel.
///  5. Checkouts nobody paid.
///  6. WhatsApp messages beyond the plan, billed once per monthly window.
/// Each step is idempotent, so a second instance running it at the same time is harmless.
/// </summary>
public class BillingMaintenanceService(
    FojiDbContext db,
    AsaasClient asaas,
    BillingOperations ops,
    BillingSettings settings,
    IEmailService email,
    ILogger<BillingMaintenanceService> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        await Step("period ends", EndCanceledPeriodsAsync, ct);
        await Step("pending downgrades", ApplyDueDowngradesAsync, ct);
        await Step("grace period", SuspendOverdueAsync, ct);
        await Step("give up", CancelLongUnpaidAsync, ct);
        await Step("stale checkouts", ExpireStaleAsync, ct);
        if (asaas.IsConfigured) await Step("whatsapp overage", BillOverageAsync, ct);
    }

    private async Task Step(string name, Func<CancellationToken, Task> step, CancellationToken ct)
    {
        try
        {
            db.ChangeTracker.Clear();
            await step(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Billing sweep step '{Step}' failed", name);
        }
    }

    private async Task EndCanceledPeriodsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var ending = await db.Subscriptions
            .Where(s => s.CancelAtPeriodEnd && s.CurrentPeriodEnd < now
                        && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue))
            .ToListAsync(ct);

        foreach (var sub in ending)
        {
            // A replacement (new card, Pix renewal, resumed plan) is charged on this
            // date: keep serving until its payment lands, at most 3 days.
            var waitingForReplacement = await db.Subscriptions.AnyAsync(s =>
                s.CompanyId == sub.CompanyId && s.Id != sub.Id && s.Status == SubscriptionStatus.Incomplete, ct);
            if (waitingForReplacement && now < sub.CurrentPeriodEnd!.Value.AddDays(3)) continue;

            sub.Status = SubscriptionStatus.Canceled;
            sub.CancelAtPeriodEnd = false;
            sub.CanceledAt ??= now;
            logger.LogInformation("Subscription {Id} of company {CompanyId} ended (canceled at period end)", sub.Id, sub.CompanyId);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyDueDowngradesAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var due = await db.Subscriptions
            .Where(s => s.PendingPlanId != null && s.CurrentPeriodEnd < now && s.Status != SubscriptionStatus.Canceled)
            .Include(s => s.PendingPlan)
            .ToListAsync(ct);
        foreach (var sub in due)
        {
            sub.PlanId = sub.PendingPlanId!.Value;
            sub.Price = BillingOperations.PriceFor(sub.PendingPlan!, sub.Cycle);
            sub.PendingPlanId = null;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SuspendOverdueAsync(CancellationToken ct)
    {
        var limit = DateTime.UtcNow.AddDays(-settings.GraceDays);
        var overdue = await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.PastDue && s.PastDueSince < limit)
            .ToListAsync(ct);

        foreach (var sub in overdue)
        {
            sub.Status = SubscriptionStatus.Unpaid;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Subscription {Id} of company {CompanyId} suspended after the grace period", sub.Id, sub.CompanyId);

            var invoice = await OpenInvoiceAsync(sub.Id, ct);
            await ops.NotifyOwnerAsync(sub.CompanyId, (owner, company) =>
                email.SendAccessSuspendedAsync(owner.Email, owner.FirstName, company.Name, invoice), ct);
        }
    }

    private async Task CancelLongUnpaidAsync(CancellationToken ct)
    {
        var limit = DateTime.UtcNow.AddDays(-settings.CancelAfterDays);
        var dead = await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Unpaid && s.PastDueSince < limit)
            .ToListAsync(ct);

        foreach (var sub in dead)
        {
            if (!string.IsNullOrEmpty(sub.AsaasSubscriptionId))
                await asaas.DeleteSubscriptionAsync(sub.AsaasSubscriptionId, ct);
            sub.Status = SubscriptionStatus.Canceled;
            sub.CanceledAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Subscription {Id} of company {CompanyId} canceled after {Days} days unpaid",
                sub.Id, sub.CompanyId, settings.CancelAfterDays);

            await ops.NotifyOwnerAsync(sub.CompanyId, (owner, company) =>
                email.SendSubscriptionCancelledAsync(owner.Email, owner.FirstName, company.Name, null), ct);
        }
    }

    private async Task ExpireStaleAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = BillingMath.TodayInBrasilia(now);

        // Hosted checkouts expire at Asaas after an hour; mark ours if the webhook never came.
        var oldCheckouts = await db.BillingCheckouts
            .Where(c => c.Status == BillingCheckoutStatus.Pending && c.AsaasCheckoutId != null
                        && c.AsaasSubscriptionId == null && c.CreatedAt < now.AddDays(-1))
            .ToListAsync(ct);
        foreach (var c in oldCheckouts) c.Status = BillingCheckoutStatus.Expired;
        await db.SaveChangesAsync(ct);

        // Pix subscriptions whose first charge was never paid, a week after it was due.
        var unpaid = await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Incomplete && s.CreatedAt < now.AddDays(-7))
            .ToListAsync(ct);
        foreach (var sub in unpaid)
        {
            var start = await db.BillingCheckouts
                .Where(c => c.AsaasSubscriptionId == sub.AsaasSubscriptionId)
                .Select(c => c.StartDate).FirstOrDefaultAsync(ct);
            if (start is { } s && s.AddDays(7) > today) continue;

            if (!string.IsNullOrEmpty(sub.AsaasSubscriptionId))
                await asaas.DeleteSubscriptionAsync(sub.AsaasSubscriptionId, ct);
            sub.Status = SubscriptionStatus.Canceled;
            sub.CanceledAt = now;
            await db.SaveChangesAsync(ct);
        }
    }

    // ── WhatsApp overage ─────────────────────────────────────────────────────

    /// <summary>
    /// Bills the monthly usage window that ended most recently, once (a unique index on
    /// company + window start guarantees it). Small amounts are recorded as waived.
    /// </summary>
    private async Task BillOverageAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = BillingMath.TodayInBrasilia(now);

        var subs = await db.Subscriptions
            .Include(s => s.Plan).Include(s => s.Company)
            .Where(s => (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue)
                        && s.AsaasSubscriptionId != null && s.CurrentPeriodStart != null
                        && s.Plan.HasWhatsApp && s.Plan.WhatsAppOverageCentavos > 0 && s.Plan.WhatsAppMessagesPerMonth >= 0)
            .ToListAsync(ct);

        foreach (var sub in subs)
        {
            var anchor = DateOnly.FromDateTime(sub.CurrentPeriodStart!.Value.Add(BillingMath.BrasiliaOffset));
            var current = BillingMath.UsageWindow(anchor, today);
            var previous = BillingMath.UsageWindow(anchor, current.Start.AddDays(-1));

            // Only windows this paid subscription fully covered.
            var since = DateOnly.FromDateTime(sub.CreatedAt.Add(BillingMath.BrasiliaOffset));
            if (previous.Start < since) continue;

            var billed = await db.BillingPayments.AnyAsync(p =>
                p.CompanyId == sub.CompanyId && p.Kind == BillingPaymentKind.Overage && p.PeriodStart == previous.Start, ct);
            if (billed) continue;

            var used = await db.WhatsAppUsageDays
                .Where(u => u.CompanyId == sub.CompanyId && u.Date >= previous.Start && u.Date < previous.End)
                .SumAsync(u => u.ServiceMessages + u.UtilityMessages + u.MarketingMessages, ct);
            var allowance = sub.Plan.WhatsAppMessagesPerMonth;
            var amount = BillingMath.Overage(used, allowance, sub.Plan.WhatsAppOverageCentavos);
            if (amount <= 0) continue;

            var extra = used - allowance;
            var description = $"Foji AI · {extra} mensagens extras do WhatsApp ({previous.Start:dd/MM} a {previous.End.AddDays(-1):dd/MM})";
            var row = new BillingPayment
            {
                CompanyId = sub.CompanyId,
                SubscriptionId = sub.Id,
                Kind = BillingPaymentKind.Overage,
                Status = amount < settings.MinChargeValue ? BillingPaymentStatus.Waived : BillingPaymentStatus.Pending,
                Value = amount,
                DueDate = today,
                PeriodStart = previous.Start,
                PeriodEnd = previous.End,
                Description = description,
            };
            db.BillingPayments.Add(row);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear(); // another instance got there first
                continue;
            }
            if (row.Status == BillingPaymentStatus.Waived) continue;

            try
            {
                await ChargeOverageAsync(sub, row, ct);
            }
            catch (Exception ex)
            {
                // Remove the marker so the next sweep tries again.
                logger.LogError(ex, "Overage charge for company {CompanyId} failed", sub.CompanyId);
                db.BillingPayments.Remove(row);
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task ChargeOverageAsync(Subscription sub, BillingPayment row, CancellationToken ct)
    {
        var customerId = await ops.EnsureCustomerAsync(sub.Company, ct);
        var reference = settings.OverageReference(row.Id);
        var today = row.DueDate.ToString("yyyy-MM-dd");
        AsaasPayment? payment = null;

        if (sub.PaymentMethod == BillingPaymentMethod.CreditCard && asaas.TokenizationEnabled && !string.IsNullOrEmpty(sub.CardToken))
        {
            try
            {
                payment = await asaas.CreatePaymentAsync(new AsaasPaymentRequest(
                    customerId, "CREDIT_CARD", row.Value, today, row.Description, reference, CreditCardToken: sub.CardToken), ct);
            }
            catch (AsaasException ex)
            {
                logger.LogInformation("Saved card refused the overage of company {CompanyId}: {Code}", sub.CompanyId, ex.Code);
            }
        }

        payment ??= await asaas.CreatePaymentAsync(new AsaasPaymentRequest(
            customerId, "UNDEFINED", row.Value, row.DueDate.AddDays(5).ToString("yyyy-MM-dd"), row.Description, reference), ct);

        row.AsaasPaymentId = payment.Id;
        row.InvoiceUrl = payment.InvoiceUrl;
        row.BillingType = payment.BillingType;
        if (DateOnly.TryParse(payment.DueDate, out var due)) row.DueDate = due;
        row.Status = BillingWebhookService.MapPaymentStatus(payment.Status);
        if (row.Status is BillingPaymentStatus.Confirmed or BillingPaymentStatus.Received)
        {
            row.PaidAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await ops.ScheduleOneOffNfseAsync(payment, row.Description!, ct);
            return;
        }

        row.NotifiedCreatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        if (row.InvoiceUrl is { } url)
        {
            var (value, dueDate, text) = (row.Value, row.DueDate, row.Description!);
            await ops.NotifyOwnerAsync(sub.CompanyId, (owner, company) => email.SendBillingInvoiceAsync(
                owner.Email, owner.FirstName, "Mensagens extras do WhatsApp",
                $"No último mês o atendente de <strong>{company.Name}</strong> mandou mais mensagens no WhatsApp do que o seu plano inclui. {text}.",
                value, dueDate, url), ct);
        }
    }

    private Task<string?> OpenInvoiceAsync(int subscriptionId, CancellationToken ct) =>
        db.BillingPayments
            .Where(p => p.SubscriptionId == subscriptionId && p.Status == BillingPaymentStatus.Overdue && p.InvoiceUrl != null)
            .OrderBy(p => p.DueDate)
            .Select(p => p.InvoiceUrl)
            .FirstOrDefaultAsync(ct);
}
