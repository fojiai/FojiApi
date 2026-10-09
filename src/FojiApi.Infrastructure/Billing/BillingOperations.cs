using FojiApi.Core.Billing;
using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Asaas;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Billing;

/// <summary>
/// The billing steps shared by customer actions (BillingService), Asaas webhooks
/// (BillingWebhookService) and the periodic sweep (BillingMaintenanceService).
/// </summary>
public class BillingOperations(
    FojiDbContext db,
    AsaasClient asaas,
    BillingSettings settings,
    IEmailService email,
    ILogger<BillingOperations> logger)
{
    public static decimal PriceFor(Plan plan, BillingCycle cycle) =>
        BillingMath.PriceFor(plan.MonthlyPrice, plan.YearlyPrice, cycle);

    public Task<User?> OwnerAsync(int companyId, CancellationToken ct = default) =>
        db.UserCompanies
            .Where(uc => uc.CompanyId == companyId && uc.Role == CompanyRole.Owner && uc.IsActive)
            .Select(uc => uc.User)
            .FirstOrDefaultAsync(ct);

    /// <summary>Emails the owner; a failed email never fails the billing step.</summary>
    public async Task NotifyOwnerAsync(int companyId, Func<User, Company, Task> send, CancellationToken ct = default)
    {
        try
        {
            var owner = await OwnerAsync(companyId, ct);
            var company = await db.Companies.FindAsync([companyId], ct);
            if (owner is null || company is null) return;
            await send(owner, company);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Billing email to the owner of company {CompanyId} failed", companyId);
        }
    }

    // ── Customer ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The Asaas customer for this company, created on first use. Asaas allows
    /// duplicate customers, so look it up by our reference before creating.
    /// Asaas's own emails/SMS/robocalls are disabled: we send our own.
    /// </summary>
    public async Task<string> EnsureCustomerAsync(Company company, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(company.AsaasCustomerId)) return company.AsaasCustomerId;

        if (!BillingMath.IsValidCpfCnpj(company.CpfCnpj))
            throw new DomainException("Preencha o CPF ou CNPJ de quem paga antes de assinar.");

        var reference = settings.CustomerReference(company.Id);
        var owner = await OwnerAsync(company.Id, ct);
        var existing = await asaas.FindCustomerByReferenceAsync(reference, ct);
        var customer = existing ?? await asaas.CreateCustomerAsync(
            new AsaasCustomerRequest(BillingName(company), company.CpfCnpj!, owner?.Email, reference), ct);

        company.AsaasCustomerId = customer.Id;
        await db.SaveChangesAsync(ct);
        return customer.Id;
    }

    /// <summary>Keeps the Asaas customer in step with the billing profile (name and CPF/CNPJ print on the NFS-e).</summary>
    public async Task SyncCustomerAsync(Company company, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(company.AsaasCustomerId) || !BillingMath.IsValidCpfCnpj(company.CpfCnpj)) return;
        var owner = await OwnerAsync(company.Id, ct);
        await asaas.UpdateCustomerAsync(company.AsaasCustomerId, new AsaasCustomerRequest(
            BillingName(company), company.CpfCnpj!, owner?.Email, settings.CustomerReference(company.Id)), ct);
    }

    private static string BillingName(Company company) =>
        company.AccountType == AccountType.Individual && !string.IsNullOrWhiteSpace(company.TradeName)
            ? company.TradeName!
            : company.Name;

    // ── Linking Asaas subscriptions back to us ───────────────────────────────

    /// <summary>
    /// Our row for an Asaas subscription. Subscriptions born from a hosted checkout
    /// are only known through webhooks, which can arrive in any order, so an unknown
    /// id is resolved through Asaas (checkoutSession / externalReference).
    /// Returns null for subscriptions that aren't ours (shared Asaas account).
    /// </summary>
    public async Task<Subscription?> FindOrLinkSubscriptionAsync(string asaasSubscriptionId, CancellationToken ct = default)
    {
        var local = await db.Subscriptions.FirstOrDefaultAsync(s => s.AsaasSubscriptionId == asaasSubscriptionId, ct);
        if (local is not null) return local;

        var remote = await asaas.GetSubscriptionAsync(asaasSubscriptionId, ct);
        if (remote is null) return null;

        BillingCheckout? checkout = null;
        if (!string.IsNullOrEmpty(remote.CheckoutSession))
            checkout = await db.BillingCheckouts.FirstOrDefaultAsync(c => c.AsaasCheckoutId == remote.CheckoutSession, ct);
        if (checkout is null && settings.ParseCheckoutReference(remote.ExternalReference) is int checkoutId)
            checkout = await db.BillingCheckouts.FindAsync([checkoutId], ct);

        return checkout is null ? null : await LinkAsync(checkout, asaasSubscriptionId, ct);
    }

    /// <summary>
    /// Creates our (Incomplete) row for a new Asaas subscription and retires the one it
    /// replaces, at Asaas too, so nobody is charged twice for the same period.
    /// </summary>
    public async Task<Subscription> LinkAsync(BillingCheckout checkout, string asaasSubscriptionId, CancellationToken ct = default)
    {
        var existing = await db.Subscriptions.FirstOrDefaultAsync(s => s.AsaasSubscriptionId == asaasSubscriptionId, ct);
        if (existing is not null) return existing;

        var sub = new Subscription
        {
            CompanyId = checkout.CompanyId,
            PlanId = checkout.PlanId,
            Status = SubscriptionStatus.Incomplete,
            Cycle = checkout.Cycle,
            PaymentMethod = checkout.Method,
            AsaasSubscriptionId = asaasSubscriptionId,
            Price = checkout.Amount,
        };
        db.Subscriptions.Add(sub);
        checkout.AsaasSubscriptionId = asaasSubscriptionId;

        if (checkout.ReplacesSubscriptionId is int oldId
            && await db.Subscriptions.FindAsync([oldId], ct) is { } old
            && old.AsaasSubscriptionId != asaasSubscriptionId)
        {
            await RetireAsync(old, ct);
        }

        await db.SaveChangesAsync(ct);
        await ConfigureSubscriptionNfseAsync(asaasSubscriptionId, ct);
        logger.LogInformation("Linked Asaas subscription {AsaasId} to company {CompanyId} (checkout {CheckoutId})",
            asaasSubscriptionId, checkout.CompanyId, checkout.Id);
        return sub;
    }

    /// <summary>
    /// Stops a subscription that another one replaces: no more charges at Asaas, and
    /// access continues until the end of what was already paid.
    /// </summary>
    private async Task RetireAsync(Subscription old, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(old.AsaasSubscriptionId))
            await asaas.DeleteSubscriptionAsync(old.AsaasSubscriptionId, ct);

        var paidAhead = old.Status == SubscriptionStatus.Active && old.CurrentPeriodEnd > DateTime.UtcNow;
        if (paidAhead)
        {
            old.CancelAtPeriodEnd = true;
        }
        else if (old.Status != SubscriptionStatus.Canceled)
        {
            old.Status = SubscriptionStatus.Canceled;
            old.CanceledAt = DateTime.UtcNow;
        }
        old.PendingPlanId = null;
    }

    // ── Payments arriving ────────────────────────────────────────────────────

    /// <summary>
    /// A subscription charge was paid: (re)activate and extend the period from the
    /// charge's due date. Idempotent: the period only ever moves forward, so a
    /// CONFIRMED followed by RECEIVED (card, boleto) changes nothing the second time.
    /// </summary>
    public async Task ActivateFromPaymentAsync(Subscription sub, AsaasPayment payment, CancellationToken ct = default)
    {
        if (!DateOnly.TryParse(payment.DueDate, out var due)) due = BillingMath.TodayInBrasilia(DateTime.UtcNow);

        var wasIncomplete = sub.Status == SubscriptionStatus.Incomplete;
        var start = BillingMath.StartOfDayUtc(due);
        var end = BillingMath.StartOfDayUtc(BillingMath.AddCycle(due, sub.Cycle));
        var previousEnd = sub.CurrentPeriodEnd;

        if (previousEnd is null || end > previousEnd)
        {
            // A scheduled downgrade starts with the first charge of the next period.
            if (sub.PendingPlanId is int pendingId && previousEnd is { } prev && start >= prev.AddDays(-1))
            {
                var plan = await db.Plans.FindAsync([pendingId], ct);
                if (plan is not null)
                {
                    sub.PlanId = plan.Id;
                    sub.Price = PriceFor(plan, sub.Cycle);
                }
                sub.PendingPlanId = null;
            }
            sub.CurrentPeriodStart = start;
            sub.CurrentPeriodEnd = end;
        }

        if (payment.CreditCard is { } card)
        {
            sub.CardToken = card.CreditCardToken ?? sub.CardToken;
            sub.CardBrand = card.CreditCardBrand ?? sub.CardBrand;
            if (BillingMath.DigitsOnly(card.CreditCardNumber) is { Length: >= 4 } digits) sub.CardLast4 = digits[^4..];
        }

        // Still owing an older charge? Stay past due until that one is paid too.
        var stillOverdue = await db.BillingPayments.AnyAsync(p =>
            p.SubscriptionId == sub.Id && p.Status == BillingPaymentStatus.Overdue
            && p.AsaasPaymentId != payment.Id, ct);
        if (stillOverdue && sub.Status is SubscriptionStatus.PastDue or SubscriptionStatus.Unpaid)
        {
            // keep PastDue/Unpaid and the original PastDueSince
        }
        else
        {
            sub.Status = SubscriptionStatus.Active;
            sub.PastDueSince = null;
        }

        if (wasIncomplete)
        {
            // The new subscription is paid: everything else this company had ends now.
            var others = await db.Subscriptions
                .Where(s => s.CompanyId == sub.CompanyId && s.Id != sub.Id && s.Status != SubscriptionStatus.Canceled)
                .ToListAsync(ct);
            foreach (var other in others)
            {
                if (!string.IsNullOrEmpty(other.AsaasSubscriptionId))
                    await asaas.DeleteSubscriptionAsync(other.AsaasSubscriptionId, ct);
                other.Status = SubscriptionStatus.Canceled;
                other.CanceledAt = DateTime.UtcNow;
                other.CancelAtPeriodEnd = false;
            }

            var checkouts = await db.BillingCheckouts
                .Where(c => c.AsaasSubscriptionId == sub.AsaasSubscriptionId && c.Status == BillingCheckoutStatus.Pending)
                .ToListAsync(ct);
            foreach (var c in checkouts)
            {
                c.Status = BillingCheckoutStatus.Completed;
                c.CompletedAt = DateTime.UtcNow;
            }
            logger.LogInformation("Subscription {Id} of company {CompanyId} activated", sub.Id, sub.CompanyId);
        }
    }

    /// <summary>
    /// The upgrade difference was paid (or was too small to charge): switch the plan now
    /// and charge the new price from the next renewal on.
    /// </summary>
    public async Task ApplyUpgradeAsync(Subscription sub, int planId, CancellationToken ct = default)
    {
        var plan = await db.Plans.FindAsync([planId], ct) ?? throw new NotFoundException("Plan not found.");
        var price = PriceFor(plan, sub.Cycle);
        sub.PlanId = plan.Id;
        sub.Price = price;
        sub.PendingPlanId = null;
        await db.SaveChangesAsync(ct);

        if (string.IsNullOrEmpty(sub.AsaasSubscriptionId)) return;
        try
        {
            await asaas.UpdateSubscriptionValueAsync(sub.AsaasSubscriptionId, price, Description(plan, sub.Cycle), ct);
        }
        catch (Exception ex)
        {
            // The customer paid; never take the upgrade back. Flag it so someone fixes
            // the price at Asaas (card subscriptions need tokenization for this).
            logger.LogError(ex, "Upgrade applied but Asaas price update failed for subscription {Id}", sub.Id);
            sub.AdminNotes = Trim($"{sub.AdminNotes}\n[{DateTime.UtcNow:yyyy-MM-dd}] Atualizar valor na Asaas para {price:0.00}: {ex.Message}", 1000);
            await db.SaveChangesAsync(ct);
        }
    }

    public static string Description(Plan plan, BillingCycle cycle) =>
        $"Foji AI · Plano {plan.Name} ({(cycle == BillingCycle.Yearly ? "anual" : "mensal")})";

    // ── NFS-e ────────────────────────────────────────────────────────────────

    /// <summary>Tells Asaas to issue the NFS-e for every charge of this subscription once it's paid.</summary>
    public async Task ConfigureSubscriptionNfseAsync(string asaasSubscriptionId, CancellationToken ct = default)
    {
        if (!settings.NfseEnabled) return;
        try
        {
            await asaas.SetSubscriptionInvoiceSettingsAsync(asaasSubscriptionId, new
            {
                municipalServiceCode = settings.NfseServiceCode,
                municipalServiceName = settings.NfseServiceName,
                effectiveDatePeriod = "ON_PAYMENT_CONFIRMATION",
                receivedOnly = false,
                observations = settings.NfseDescription,
                taxes = settings.NfseTaxes,
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not configure NFS-e for Asaas subscription {Id}", asaasSubscriptionId);
        }
    }

    /// <summary>NFS-e for a one-off charge (upgrade difference, WhatsApp overage).</summary>
    public async Task ScheduleOneOffNfseAsync(AsaasPayment payment, string description, CancellationToken ct = default)
    {
        if (!settings.NfseEnabled) return;
        try
        {
            await asaas.ScheduleInvoiceAsync(new
            {
                payment = payment.Id,
                serviceDescription = description,
                observations = settings.NfseDescription,
                value = payment.Value,
                deductions = 0,
                effectiveDate = BillingMath.TodayInBrasilia(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                municipalServiceCode = settings.NfseServiceCode,
                municipalServiceName = settings.NfseServiceName,
                taxes = settings.NfseTaxes,
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not schedule the NFS-e for Asaas payment {Id}", payment.Id);
        }
    }

    private static string Trim(string text, int max) => text.Length <= max ? text : text[^max..];
}
