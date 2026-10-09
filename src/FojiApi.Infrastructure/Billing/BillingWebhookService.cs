using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
/// Asaas webhooks. Ingest stores the event and returns (Asaas allows 10s and pauses
/// the whole queue after 15 failures); ProcessPendingAsync applies them in order
/// from a background job, with retries. Asaas delivers at least once, so the event
/// id is unique and every handler is idempotent.
/// </summary>
public class BillingWebhookService(
    FojiDbContext db,
    BillingOperations ops,
    BillingSettings settings,
    IEmailService email,
    ILogger<BillingWebhookService> logger)
{
    private const int MaxAttempts = 10;

    public static BillingPaymentStatus MapPaymentStatus(string? status) => status switch
    {
        "CONFIRMED" => BillingPaymentStatus.Confirmed,
        "RECEIVED" or "RECEIVED_IN_CASH" => BillingPaymentStatus.Received,
        "OVERDUE" => BillingPaymentStatus.Overdue,
        "REFUNDED" or "PARTIALLY_REFUNDED" or "CHARGEBACK_REQUESTED" or "CHARGEBACK_DISPUTE" => BillingPaymentStatus.Refunded,
        _ => BillingPaymentStatus.Pending,
    };

    // ── Ingest ───────────────────────────────────────────────────────────────

    /// <returns>false when the token is wrong (the caller answers 401).</returns>
    public async Task<bool> IngestAsync(string payload, string? token, CancellationToken ct = default)
    {
        var expected = settings.WebhookToken;
        if (string.IsNullOrEmpty(expected) || token is null
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(token)))
        {
            logger.LogWarning("Asaas webhook rejected: bad or missing asaas-access-token");
            return false;
        }

        string eventId, eventName;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            eventId = doc.RootElement.GetProperty("id").GetString() ?? throw new JsonException("no id");
            eventName = doc.RootElement.GetProperty("event").GetString() ?? throw new JsonException("no event");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Not something we can ever process. Answer 200 so the queue doesn't stall on it.
            logger.LogWarning("Asaas webhook with an unreadable body ignored: {Error}", ex.Message);
            return true;
        }

        if (await db.BillingWebhookEvents.AnyAsync(e => e.EventId == eventId, ct)) return true;

        db.BillingWebhookEvents.Add(new BillingWebhookEvent
        {
            EventId = eventId,
            Event = eventName,
            Payload = payload,
            ReceivedAt = DateTime.UtcNow,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two deliveries of the same event raced; the other one stored it.
            db.ChangeTracker.Clear();
        }
        return true;
    }

    // ── Processing ───────────────────────────────────────────────────────────

    public async Task<int> ProcessPendingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var ids = await db.BillingWebhookEvents
            .Where(e => e.ProcessedAt == null
                        && (e.NextAttemptAt == null || e.NextAttemptAt <= now)
                        && (e.LockedUntil == null || e.LockedUntil < now))
            .OrderBy(e => e.Id)
            .Select(e => e.Id)
            .Take(25)
            .ToListAsync(ct);

        var done = 0;
        foreach (var id in ids)
        {
            // Claim it, so another API instance running this job skips it.
            var claimed = await db.BillingWebhookEvents
                .Where(e => e.Id == id && e.ProcessedAt == null && (e.LockedUntil == null || e.LockedUntil < DateTime.UtcNow))
                .ExecuteUpdateAsync(u => u.SetProperty(e => e.LockedUntil, DateTime.UtcNow.AddMinutes(2)), ct);
            if (claimed == 0) continue;

            db.ChangeTracker.Clear();
            var evt = await db.BillingWebhookEvents.FirstAsync(e => e.Id == id, ct);
            try
            {
                await ProcessAsync(evt.Event, evt.Payload, ct);
                db.ChangeTracker.Clear();
                evt = await db.BillingWebhookEvents.FirstAsync(e => e.Id == id, ct);
                evt.ProcessedAt = DateTime.UtcNow;
                evt.LastError = null;
                done++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                evt = await db.BillingWebhookEvents.FirstAsync(e => e.Id == id, ct);
                evt.Attempts++;
                evt.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                evt.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, evt.Attempts)));
                if (evt.Attempts >= MaxAttempts) evt.ProcessedAt = DateTime.UtcNow;
                logger.LogError(ex, "Asaas event {EventId} ({Event}) failed, attempt {Attempt}", evt.EventId, evt.Event, evt.Attempts);
            }
            evt.LockedUntil = null;
            await db.SaveChangesAsync(ct);
        }
        return done;
    }

    private async Task ProcessAsync(string evt, string payload, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        if (evt.StartsWith("PAYMENT_") && root.TryGetProperty("payment", out var p))
            await HandlePaymentAsync(evt, p.Deserialize<AsaasPayment>(AsaasClient.Json)!, ct);
        else if (evt.StartsWith("SUBSCRIPTION_") && root.TryGetProperty("subscription", out var s))
            await HandleSubscriptionAsync(evt, s.Deserialize<AsaasSubscription>(AsaasClient.Json)!, ct);
        else if (evt.StartsWith("CHECKOUT_") && root.TryGetProperty("checkout", out var c))
            await HandleCheckoutAsync(evt, c.TryGetProperty("id", out var cid) ? cid.GetString() : null, ct);
        else if (evt == "INVOICE_AUTHORIZED" && root.TryGetProperty("invoice", out var inv))
            await HandleInvoiceAsync(inv, ct);
        // Everything else (transfers, account status, other products' events) is not ours to act on.
    }

    private async Task HandlePaymentAsync(string evt, AsaasPayment p, CancellationToken ct)
    {
        var row = await db.BillingPayments.FirstOrDefaultAsync(x => x.AsaasPaymentId == p.Id, ct);
        Subscription? sub = null;
        BillingCheckout? checkout = null;
        BillingPaymentKind kind;

        if (!string.IsNullOrEmpty(p.Subscription))
        {
            sub = await ops.FindOrLinkSubscriptionAsync(p.Subscription, ct);
            if (sub is null) return; // another product's subscription
            kind = BillingPaymentKind.Subscription;
        }
        else if (settings.ParseCheckoutReference(p.ExternalReference) is int checkoutId)
        {
            checkout = await db.BillingCheckouts.FindAsync([checkoutId], ct);
            if (checkout is null) return;
            kind = BillingPaymentKind.Upgrade;
            if (checkout.SubscriptionId is int sid) sub = await db.Subscriptions.FindAsync([sid], ct);
        }
        else if (settings.ParseOverageReference(p.ExternalReference) is int overageId)
        {
            row ??= await db.BillingPayments.FindAsync([overageId], ct);
            if (row is null) return;
            kind = BillingPaymentKind.Overage;
            if (row.SubscriptionId is int sid) sub = await db.Subscriptions.FindAsync([sid], ct);
        }
        else return;

        var companyId = sub?.CompanyId ?? checkout?.CompanyId ?? row!.CompanyId;
        if (row is null)
        {
            row = new BillingPayment
            {
                CompanyId = companyId,
                SubscriptionId = sub?.Id,
                Kind = kind,
                Description = p.Description,
            };
            db.BillingPayments.Add(row);
        }

        row.AsaasPaymentId = p.Id;
        row.Value = p.Value;
        row.BillingType = p.BillingType ?? row.BillingType;
        row.InvoiceUrl = p.InvoiceUrl ?? row.InvoiceUrl;
        if (DateOnly.TryParse(p.DueDate, out var due)) row.DueDate = due;
        row.SubscriptionId ??= sub?.Id;

        var paid = evt is "PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED";
        var firstPaid = paid && row.PaidAt is null;
        row.Status = evt switch
        {
            "PAYMENT_RECEIVED" => BillingPaymentStatus.Received,
            "PAYMENT_CONFIRMED" => row.Status == BillingPaymentStatus.Received ? BillingPaymentStatus.Received : BillingPaymentStatus.Confirmed,
            "PAYMENT_OVERDUE" => BillingPaymentStatus.Overdue,
            "PAYMENT_DELETED" => BillingPaymentStatus.Deleted,
            "PAYMENT_REFUNDED" or "PAYMENT_PARTIALLY_REFUNDED" => BillingPaymentStatus.Refunded,
            _ => row.PaidAt is null ? MapPaymentStatus(p.Status) : row.Status,
        };
        if (firstPaid) row.PaidAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (firstPaid)
        {
            switch (kind)
            {
                case BillingPaymentKind.Subscription:
                    await ops.ActivateFromPaymentAsync(sub!, p, ct);
                    await db.SaveChangesAsync(ct);
                    break;
                case BillingPaymentKind.Upgrade:
                    if (checkout!.Status == BillingCheckoutStatus.Pending)
                    {
                        checkout.Status = BillingCheckoutStatus.Completed;
                        checkout.CompletedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(ct);
                    }
                    if (sub is not null && sub.PlanId != checkout.PlanId) await ops.ApplyUpgradeAsync(sub, checkout.PlanId, ct);
                    await ops.ScheduleOneOffNfseAsync(p, row.Description ?? "Foji AI · Troca de plano", ct);
                    break;
                case BillingPaymentKind.Overage:
                    await ops.ScheduleOneOffNfseAsync(p, row.Description ?? "Foji AI · Mensagens extras do WhatsApp", ct);
                    break;
            }
            return;
        }

        var notifiable = kind != BillingPaymentKind.Upgrade && sub?.Status != SubscriptionStatus.Incomplete;

        if (evt == "PAYMENT_OVERDUE")
        {
            if (kind == BillingPaymentKind.Subscription && sub!.Status == SubscriptionStatus.Active)
            {
                sub.Status = SubscriptionStatus.PastDue;
                sub.PastDueSince ??= DateTime.UtcNow;
            }
            if (notifiable && row.NotifiedOverdueAt is null)
            {
                row.NotifiedOverdueAt = DateTime.UtcNow;
                var suspends = sub?.PastDueSince?.AddDays(settings.GraceDays);
                var url = row.InvoiceUrl;
                await ops.NotifyOwnerAsync(companyId, (owner, company) =>
                    email.SendPaymentFailedAsync(owner.Email, owner.FirstName, company.Name, url, cardRefused: false, suspends), ct);
            }
            await db.SaveChangesAsync(ct);
        }
        else if (evt == "PAYMENT_CREDIT_CARD_CAPTURE_REFUSED" && kind == BillingPaymentKind.Subscription && notifiable
                 && row.NotifiedOverdueAt is null)
        {
            row.NotifiedOverdueAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            var url = row.InvoiceUrl;
            await ops.NotifyOwnerAsync(companyId, (owner, company) =>
                email.SendPaymentFailedAsync(owner.Email, owner.FirstName, company.Name, url, cardRefused: true, null), ct);
        }
        else if (evt == "PAYMENT_CREATED" && kind == BillingPaymentKind.Subscription
                 && sub!.PaymentMethod == BillingPaymentMethod.Pix && sub.Status != SubscriptionStatus.Incomplete
                 && row.NotifiedCreatedAt is null && row.InvoiceUrl is { } invoiceUrl)
        {
            // Yearly Pix renewals don't charge themselves: send the payment link.
            row.NotifiedCreatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            var value = row.Value;
            var dueDate = row.DueDate;
            await ops.NotifyOwnerAsync(companyId, (owner, company) => email.SendBillingInvoiceAsync(
                owner.Email, owner.FirstName, "Sua renovação anual",
                $"Chegou a hora de renovar o plano anual de <strong>{company.Name}</strong>. É só pagar o Pix até o vencimento para continuar sem interrupção.",
                value, dueDate, invoiceUrl), ct);
        }
    }

    private async Task HandleSubscriptionAsync(string evt, AsaasSubscription s, CancellationToken ct)
    {
        if (evt == "SUBSCRIPTION_CREATED")
        {
            if (await db.Subscriptions.AnyAsync(x => x.AsaasSubscriptionId == s.Id, ct)) return;
            BillingCheckout? checkout = null;
            if (!string.IsNullOrEmpty(s.CheckoutSession))
                checkout = await db.BillingCheckouts.FirstOrDefaultAsync(c => c.AsaasCheckoutId == s.CheckoutSession, ct);
            if (checkout is null && settings.ParseCheckoutReference(s.ExternalReference) is int checkoutId)
                checkout = await db.BillingCheckouts.FindAsync([checkoutId], ct);
            if (checkout is not null) await ops.LinkAsync(checkout, s.Id, ct);
            return;
        }

        if (evt is not ("SUBSCRIPTION_DELETED" or "SUBSCRIPTION_INACTIVATED")) return;

        var local = await db.Subscriptions.FirstOrDefaultAsync(x => x.AsaasSubscriptionId == s.Id, ct);
        // Ours to ignore: we deleted it ourselves (cancel, replacement, company removed).
        if (local is null || local.Status == SubscriptionStatus.Canceled || local.CancelAtPeriodEnd) return;

        // Removed at Asaas (panel or support), not through the app.
        var paidAhead = local.Status == SubscriptionStatus.Active && local.CurrentPeriodEnd > DateTime.UtcNow;
        if (local.Status == SubscriptionStatus.Incomplete)
        {
            local.Status = SubscriptionStatus.Canceled;
            local.CanceledAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }
        local.CanceledAt = DateTime.UtcNow;
        if (paidAhead) local.CancelAtPeriodEnd = true;
        else local.Status = SubscriptionStatus.Canceled;
        await db.SaveChangesAsync(ct);

        var until = paidAhead ? local.CurrentPeriodEnd : null;
        await ops.NotifyOwnerAsync(local.CompanyId, (owner, company) =>
            email.SendSubscriptionCancelledAsync(owner.Email, owner.FirstName, company.Name, until), ct);
    }

    private async Task HandleCheckoutAsync(string evt, string? asaasCheckoutId, CancellationToken ct)
    {
        if (asaasCheckoutId is null) return;
        var checkout = await db.BillingCheckouts.FirstOrDefaultAsync(c => c.AsaasCheckoutId == asaasCheckoutId, ct);
        if (checkout is null || checkout.Status != BillingCheckoutStatus.Pending) return;

        checkout.Status = evt switch
        {
            "CHECKOUT_EXPIRED" => BillingCheckoutStatus.Expired,
            "CHECKOUT_CANCELED" => BillingCheckoutStatus.Canceled,
            _ => checkout.Status, // CHECKOUT_PAID: completed when the payment activates the subscription
        };
        await db.SaveChangesAsync(ct);
    }

    private async Task HandleInvoiceAsync(JsonElement invoice, CancellationToken ct)
    {
        var paymentId = invoice.TryGetProperty("payment", out var pay) ? pay.GetString() : null;
        var pdf = invoice.TryGetProperty("pdfUrl", out var url) ? url.GetString() : null;
        if (paymentId is null || pdf is null) return;

        var row = await db.BillingPayments.FirstOrDefaultAsync(x => x.AsaasPaymentId == paymentId, ct);
        if (row is null) return;
        row.NfseUrl = pdf;
        await db.SaveChangesAsync(ct);
    }
}
