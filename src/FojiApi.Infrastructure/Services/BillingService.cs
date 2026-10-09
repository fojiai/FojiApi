using FojiApi.Core.Billing;
using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Asaas;
using FojiApi.Infrastructure.Billing;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Services;

/// <summary>
/// Customer billing actions on top of Asaas. See IBillingService for the rules.
/// Nothing here grants access by itself except free plan swaps: paid changes take
/// effect when Asaas confirms the payment (BillingWebhookService).
/// </summary>
public class BillingService(
    FojiDbContext db,
    AsaasClient asaas,
    BillingOperations ops,
    BillingSettings settings,
    IEmailService email,
    ILogger<BillingService> logger) : IBillingService
{
    // ── Reading ──────────────────────────────────────────────────────────────

    public async Task<SubscriptionResult?> GetSubscriptionAsync(int companyId)
    {
        var subs = await db.Subscriptions.Include(s => s.Plan).Include(s => s.PendingPlan)
            .Where(s => s.CompanyId == companyId).ToListAsync();
        var sub = SubscriptionSelector.PickCurrent(subs);
        if (sub is null) return null;

        var plan = settings.EnforcementEnabled ? ToPlanResult(sub.Plan) : UnlockedPlan(sub.Plan);

        string? openInvoice = null;
        if (sub.Status is SubscriptionStatus.PastDue or SubscriptionStatus.Unpaid || sub.PaymentMethod == BillingPaymentMethod.Pix)
        {
            openInvoice = await db.BillingPayments
                .Where(p => p.SubscriptionId == sub.Id && p.InvoiceUrl != null
                            && (p.Status == BillingPaymentStatus.Overdue
                                || (p.Status == BillingPaymentStatus.Pending && sub.PaymentMethod == BillingPaymentMethod.Pix)))
                .OrderBy(p => p.DueDate)
                .Select(p => p.InvoiceUrl)
                .FirstOrDefaultAsync();
        }

        var replacement = subs
            .Where(s => s.Status == SubscriptionStatus.Incomplete && s.Id != sub.Id && s.CreatedAt >= sub.CreatedAt)
            .OrderByDescending(s => s.CreatedAt).FirstOrDefault();
        DateTime? replacementStarts = null;
        if (replacement is not null)
        {
            replacementStarts = await db.BillingCheckouts
                .Where(c => c.AsaasSubscriptionId == replacement.AsaasSubscriptionId && c.StartDate != null)
                .Select(c => c.StartDate)
                .FirstOrDefaultAsync() is { } startDate ? BillingMath.StartOfDayUtc(startDate) : null;
        }

        return new SubscriptionResult(
            sub.Id,
            SubscriptionSelector.StatusName(settings.EnforcementEnabled ? sub.Status : SubscriptionStatus.Active),
            plan,
            sub.CurrentPeriodStart, sub.CurrentPeriodEnd, sub.TrialEndsAt, sub.CanceledAt,
            sub.Cycle == BillingCycle.Yearly ? "yearly" : "monthly",
            SubscriptionSelector.MethodName(sub.PaymentMethod),
            sub.Price,
            sub.CardBrand, sub.CardLast4,
            sub.CancelAtPeriodEnd,
            sub.PendingPlan is null ? null : ToPlanResult(sub.PendingPlan),
            IsPaid: !string.IsNullOrEmpty(sub.AsaasSubscriptionId),
            IsAdminAssigned: sub.AssignedByAdminId != null,
            openInvoice,
            sub.PastDueSince,
            sub.PastDueSince?.AddDays(settings.GraceDays),
            replacementStarts);
    }

    private static SubscriptionPlanResult ToPlanResult(Plan p) =>
        new(p.Id, p.Name, p.MaxAgents, p.HasWhatsApp, p.HasEscalationContacts, p.HasGoogleCalendar, p.HasCrm,
            p.MaxConversationsPerMonth, p.MaxMessagesPerMonth);

    private static SubscriptionPlanResult UnlockedPlan(Plan p) =>
        new(p.Id, p.Name, int.MaxValue, true, true, true, true, 0, 0);

    public async Task<IReadOnlyList<BillingPaymentResult>> ListPaymentsAsync(int companyId) =>
        await db.BillingPayments
            .Where(p => p.CompanyId == companyId && p.Status != BillingPaymentStatus.Deleted)
            .OrderByDescending(p => p.DueDate).ThenByDescending(p => p.Id)
            .Take(50)
            .Select(p => new BillingPaymentResult(
                p.Id,
                p.Kind.ToString().ToLower(),
                p.Status.ToString().ToLower(),
                p.Value, p.BillingType, p.DueDate, p.PaidAt, p.InvoiceUrl, p.NfseUrl, p.Description))
            .ToListAsync();

    public async Task<BillingCheckoutResult?> GetCheckoutAsync(int companyId, int checkoutId) =>
        await db.BillingCheckouts
            .Where(c => c.Id == checkoutId && c.CompanyId == companyId)
            .Select(c => new BillingCheckoutResult(c.Id, c.Kind.ToString().ToLower(), c.Status.ToString().ToLower(), c.Url))
            .FirstOrDefaultAsync();

    // ── Billing profile (who pays: required by Asaas) ────────────────────────

    public async Task<BillingProfileResult> GetProfileAsync(int companyId)
    {
        var c = await db.Companies.FindAsync(companyId) ?? throw new NotFoundException("Company not found.");
        return ToProfile(c);
    }

    public async Task<BillingProfileResult> UpdateProfileAsync(int companyId, UpdateBillingProfileRequest request)
    {
        var c = await db.Companies.FindAsync(companyId) ?? throw new NotFoundException("Company not found.");
        var digits = BillingMath.DigitsOnly(request.CpfCnpj);
        if (!BillingMath.IsValidCpfCnpj(digits))
            throw new DomainException("CPF ou CNPJ inválido. Confira os números.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainException("Informe o nome de quem paga.");

        var individual = digits.Length == 11;
        c.AccountType = individual ? AccountType.Individual : AccountType.Business;
        c.CpfCnpj = digits;
        if (individual) c.TradeName = request.Name.Trim();
        else c.Name = string.IsNullOrWhiteSpace(c.Name) ? request.Name.Trim() : c.Name;
        await db.SaveChangesAsync();

        try { await ops.SyncCustomerAsync(c); }
        catch (Exception ex) { logger.LogWarning(ex, "Could not update the Asaas customer of company {CompanyId}", companyId); }
        return ToProfile(c);
    }

    private static BillingProfileResult ToProfile(Company c) => new(
        c.AccountType == AccountType.Individual && !string.IsNullOrWhiteSpace(c.TradeName) ? c.TradeName! : c.Name,
        c.AccountType == AccountType.Individual ? "individual" : "business",
        c.CpfCnpj,
        BillingMath.IsValidCpfCnpj(c.CpfCnpj));

    // ── Choosing a plan ──────────────────────────────────────────────────────

    private sealed record Choice(Plan Plan, BillingCycle Cycle, BillingPaymentMethod Method, decimal Price);

    private void EnsureConfigured()
    {
        if (!asaas.IsConfigured)
            throw new DomainException("Os pagamentos ainda não estão ativados. Fale com o suporte da Foji.");
    }

    private async Task<Choice> ResolveChoiceAsync(int companyId, ChoosePlanRequest request)
    {
        EnsureConfigured();
        var plan = await db.Plans.FirstOrDefaultAsync(p =>
                       p.Id == request.PlanId && p.IsActive && (p.IsPublic || p.CustomForCompanyId == companyId))
                   ?? throw new NotFoundException("Plano não encontrado.");

        var cycle = request.Cycle?.ToLowerInvariant() switch
        {
            "yearly" or "annual" => BillingCycle.Yearly,
            _ => BillingCycle.Monthly,
        };
        var method = request.Method?.ToLowerInvariant() switch
        {
            "pix" => BillingPaymentMethod.Pix,
            _ => BillingPaymentMethod.CreditCard,
        };

        if (method == BillingPaymentMethod.Pix && cycle != BillingCycle.Yearly)
            throw new DomainException("Pix está disponível só no plano anual. No mensal a cobrança é no cartão.");
        if (cycle == BillingCycle.Yearly && plan.YearlyPrice is null)
            throw new DomainException("Este plano não tem opção anual.");

        var price = BillingOperations.PriceFor(plan, cycle);
        if (price <= 0) throw new DomainException("Este plano não tem preço configurado.");
        return new Choice(plan, cycle, method, price);
    }

    private async Task<Subscription?> CurrentAsync(int companyId) =>
        SubscriptionSelector.PickCurrent(await db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.CompanyId == companyId).ToListAsync());

    /// <summary>Paid through Asaas and still running.</summary>
    private static bool IsRunningPaid(Subscription? s) =>
        s is not null && !string.IsNullOrEmpty(s.AsaasSubscriptionId)
        && s.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue or SubscriptionStatus.Unpaid;

    private static string KindFor(Subscription current, Choice choice)
    {
        if (current.CancelAtPeriodEnd || current.Cycle != choice.Cycle || current.PaymentMethod != choice.Method)
            return "switch";
        if (current.PlanId == choice.Plan.Id) return "same";
        var oldPrice = current.Price ?? BillingOperations.PriceFor(current.Plan, current.Cycle);
        return choice.Price > oldPrice ? "upgrade" : "downgrade";
    }

    public async Task<PlanChangePreview> PreviewAsync(int companyId, ChoosePlanRequest request)
    {
        var choice = await ResolveChoiceAsync(companyId, request);
        var current = await CurrentAsync(companyId);
        var cycleName = choice.Cycle == BillingCycle.Yearly ? "yearly" : "monthly";

        if (!IsRunningPaid(current))
            return new PlanChangePreview("new", choice.Price, choice.Price, cycleName, null);

        var kind = KindFor(current!, choice);
        return kind switch
        {
            "upgrade" => new PlanChangePreview(kind, UpgradeAmount(current!, choice), choice.Price, cycleName, null),
            "downgrade" or "switch" => new PlanChangePreview(kind, 0, choice.Price, cycleName, NextStart(current!)),
            _ => new PlanChangePreview(kind, 0, choice.Price, cycleName, null),
        };
    }

    private decimal UpgradeAmount(Subscription current, Choice choice)
    {
        if (current.CurrentPeriodStart is not { } start || current.CurrentPeriodEnd is not { } end) return 0;
        var oldPrice = current.Price ?? BillingOperations.PriceFor(current.Plan, current.Cycle);
        var amount = BillingMath.ProratedUpgrade(oldPrice, choice.Price, start, end, DateTime.UtcNow);
        return amount < settings.MinChargeValue ? 0 : amount;
    }

    /// <summary>When something that waits for the paid period to end would start.</summary>
    private static DateTime? NextStart(Subscription current) =>
        current.Status == SubscriptionStatus.Active && current.CurrentPeriodEnd > DateTime.UtcNow
            ? current.CurrentPeriodEnd
            : null;

    public async Task<BillingActionResult> ChoosePlanAsync(int companyId, int userId, ChoosePlanRequest request, string? remoteIp)
    {
        var choice = await ResolveChoiceAsync(companyId, request);
        var company = await db.Companies.FindAsync(companyId) ?? throw new NotFoundException("Company not found.");
        var current = await CurrentAsync(companyId);

        if (!IsRunningPaid(current))
            return await StartSubscriptionAsync(company, userId, choice, BillingCheckoutKind.NewSubscription, null, null);

        if (current!.Status is SubscriptionStatus.PastDue or SubscriptionStatus.Unpaid)
            throw new DomainException("Há uma fatura em aberto. Pague a fatura ou troque o cartão antes de mudar de plano.");

        switch (KindFor(current, choice))
        {
            case "same":
                if (current.PendingPlanId is not null) return await CancelPendingChangeAsync(companyId);
                throw new DomainException("Você já está neste plano.");

            case "upgrade":
                return await UpgradeAsync(company, current, choice, userId, remoteIp);

            case "downgrade":
            {
                if (choice.Price == (current.Price ?? 0))
                {
                    // Same price, different plan: nothing to charge, swap now.
                    await ops.ApplyUpgradeAsync(current, choice.Plan.Id);
                    return new BillingActionResult("applied");
                }
                await asaas.UpdateSubscriptionValueAsync(current.AsaasSubscriptionId!, choice.Price,
                    BillingOperations.Description(choice.Plan, choice.Cycle));
                current.PendingPlanId = choice.Plan.Id;
                await db.SaveChangesAsync();
                return new BillingActionResult("scheduled", EffectiveAt: current.CurrentPeriodEnd);
            }

            default: // switch: other cycle or method, or coming back after canceling
            {
                var start = NextStart(current) is { } at ? DateOnly.FromDateTime(at.Add(BillingMath.BrasiliaOffset)) : (DateOnly?)null;
                return await StartSubscriptionAsync(company, userId, choice, BillingCheckoutKind.Replacement, current, start);
            }
        }
    }

    /// <summary>
    /// Sends the customer to pay for a new subscription.
    ///  - Card: Asaas hosted checkout (RECURRENT), charged automatically every cycle.
    ///  - Pix (yearly): an Asaas subscription whose first invoice we open for them.
    /// <paramref name="startDate"/> null = first charge today.
    /// </summary>
    private async Task<BillingActionResult> StartSubscriptionAsync(
        Company company, int userId, Choice choice, BillingCheckoutKind kind, Subscription? replaces, DateOnly? startDate)
    {
        var customerId = await ops.EnsureCustomerAsync(company);
        var today = BillingMath.TodayInBrasilia(DateTime.UtcNow);
        var first = startDate is { } d && d > today ? d : today;

        // Don't leave a trail of open checkouts: only the newest one can be paid.
        var stale = await db.BillingCheckouts
            .Where(c => c.CompanyId == company.Id && c.Status == BillingCheckoutStatus.Pending
                        && c.Kind != BillingCheckoutKind.Upgrade)
            .ToListAsync();
        foreach (var old in stale)
        {
            await CancelRemoteCheckoutAsync(old);
            old.Status = BillingCheckoutStatus.Canceled;
        }

        var checkout = new BillingCheckout
        {
            CompanyId = company.Id,
            Kind = kind,
            PlanId = choice.Plan.Id,
            Cycle = choice.Cycle,
            Method = choice.Method,
            Amount = choice.Price,
            ReplacesSubscriptionId = replaces?.Id,
            StartDate = first,
            CreatedByUserId = userId,
        };
        db.BillingCheckouts.Add(checkout);
        await db.SaveChangesAsync();

        var reference = settings.CheckoutReference(checkout.Id);
        var description = BillingOperations.Description(choice.Plan, choice.Cycle);

        if (choice.Method == BillingPaymentMethod.CreditCard)
        {
            var created = await asaas.CreateCheckoutAsync(new AsaasCheckoutRequest
            {
                Customer = customerId,
                ExternalReference = reference,
                Callback = Callback(checkout.Id),
                Items =
                [
                    new AsaasCheckoutItem
                    {
                        Name = Truncate($"Foji AI {choice.Plan.Name}", 30),
                        Description = Truncate(description, 150),
                        Value = choice.Price,
                    },
                ],
                Subscription = new AsaasCheckoutSubscription(
                    BillingMath.ToAsaasCycle(choice.Cycle), first.ToString("yyyy-MM-dd")),
            });
            checkout.AsaasCheckoutId = created.Id;
            checkout.Url = created.Link ?? $"https://asaas.com/checkoutSession/show?id={created.Id}";
        }
        else
        {
            var created = await asaas.CreateSubscriptionAsync(new AsaasSubscriptionRequest(
                customerId, "PIX", choice.Price, first.ToString("yyyy-MM-dd"),
                BillingMath.ToAsaasCycle(choice.Cycle), description, reference,
                new AsaasCallback(SuccessUrl(checkout.Id), AutoRedirect: true)));

            await ops.LinkAsync(checkout, created.Id);
            checkout.Url = await FirstInvoiceUrlAsync(created.Id, first)
                ?? throw new DomainException("A Asaas ainda não gerou a cobrança Pix. Tente de novo em alguns segundos.");
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Checkout {Id} ({Kind}, {Method}, {Cycle}) started for company {CompanyId}",
            checkout.Id, kind, choice.Method, choice.Cycle, company.Id);
        return new BillingActionResult("redirect", checkout.Url, CheckoutId: checkout.Id);
    }

    /// <summary>
    /// The first charge isn't in the create response: Asaas generates it right after.
    /// Pick the one due on the first date (more than one can exist: charges are created
    /// up to 40 days ahead).
    /// </summary>
    private async Task<string?> FirstInvoiceUrlAsync(string asaasSubscriptionId, DateOnly firstDue)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var payments = await asaas.ListSubscriptionPaymentsAsync(asaasSubscriptionId);
            var first = payments
                .Where(p => !string.IsNullOrEmpty(p.InvoiceUrl))
                .OrderBy(p => p.DueDate == firstDue.ToString("yyyy-MM-dd") ? 0 : 1)
                .ThenBy(p => p.DueDate)
                .FirstOrDefault();
            if (first is not null) return first.InvoiceUrl;
            await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)));
        }
        return null;
    }

    private async Task<BillingActionResult> UpgradeAsync(
        Company company, Subscription current, Choice choice, int userId, string? remoteIp)
    {
        var amount = UpgradeAmount(current, choice);
        if (amount <= 0)
        {
            await ops.ApplyUpgradeAsync(current, choice.Plan.Id);
            return new BillingActionResult("applied");
        }

        var customerId = await ops.EnsureCustomerAsync(company);
        var checkout = new BillingCheckout
        {
            CompanyId = company.Id,
            Kind = BillingCheckoutKind.Upgrade,
            PlanId = choice.Plan.Id,
            Cycle = choice.Cycle,
            Method = choice.Method,
            Amount = amount,
            SubscriptionId = current.Id,
            CreatedByUserId = userId,
        };
        db.BillingCheckouts.Add(checkout);
        await db.SaveChangesAsync();

        var reference = settings.CheckoutReference(checkout.Id);
        var description = $"Foji AI · Diferença da troca para o plano {choice.Plan.Name}";
        var today = BillingMath.TodayInBrasilia(DateTime.UtcNow).ToString("yyyy-MM-dd");

        // With a saved card (and tokenization on the Asaas account) the difference is
        // charged on the spot: no second checkout for the customer.
        if (choice.Method == BillingPaymentMethod.CreditCard && asaas.TokenizationEnabled
            && !string.IsNullOrEmpty(current.CardToken))
        {
            try
            {
                var charged = await asaas.CreatePaymentAsync(new AsaasPaymentRequest(
                    customerId, "CREDIT_CARD", amount, today, description, reference,
                    CreditCardToken: current.CardToken, RemoteIp: remoteIp));
                await RecordOneOffAsync(company.Id, current.Id, BillingPaymentKind.Upgrade, charged, description);
                checkout.AsaasPaymentId = charged.Id;

                if (charged.Status is "CONFIRMED" or "RECEIVED")
                {
                    checkout.Status = BillingCheckoutStatus.Completed;
                    checkout.CompletedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    await ops.ApplyUpgradeAsync(current, choice.Plan.Id);
                    await ops.ScheduleOneOffNfseAsync(charged, description);
                    return new BillingActionResult("applied", CheckoutId: checkout.Id);
                }
                checkout.Url = charged.InvoiceUrl;
                await db.SaveChangesAsync();
                if (!string.IsNullOrEmpty(charged.InvoiceUrl))
                    return new BillingActionResult("redirect", charged.InvoiceUrl, CheckoutId: checkout.Id);
            }
            catch (AsaasException ex)
            {
                // Card refused this time: fall through to a payment page.
                logger.LogInformation("Saved-card upgrade charge refused for company {CompanyId}: {Code}", company.Id, ex.Code);
            }
        }

        var payment = await asaas.CreatePaymentAsync(new AsaasPaymentRequest(
            customerId, "UNDEFINED", amount, today, description, reference,
            new AsaasCallback(SuccessUrl(checkout.Id), AutoRedirect: true)));
        await RecordOneOffAsync(company.Id, current.Id, BillingPaymentKind.Upgrade, payment, description);
        checkout.AsaasPaymentId = payment.Id;
        checkout.Url = payment.InvoiceUrl;
        await db.SaveChangesAsync();
        return new BillingActionResult("redirect", payment.InvoiceUrl, CheckoutId: checkout.Id);
    }

    private async Task RecordOneOffAsync(int companyId, int? subscriptionId, BillingPaymentKind kind, AsaasPayment p, string description)
    {
        if (await db.BillingPayments.AnyAsync(x => x.AsaasPaymentId == p.Id)) return;
        db.BillingPayments.Add(new BillingPayment
        {
            CompanyId = companyId,
            SubscriptionId = subscriptionId,
            Kind = kind,
            Status = BillingWebhookService.MapPaymentStatus(p.Status),
            AsaasPaymentId = p.Id,
            Value = p.Value,
            BillingType = p.BillingType,
            DueDate = DateOnly.TryParse(p.DueDate, out var due) ? due : BillingMath.TodayInBrasilia(DateTime.UtcNow),
            InvoiceUrl = p.InvoiceUrl,
            Description = description,
            PaidAt = p.Status is "CONFIRMED" or "RECEIVED" ? DateTime.UtcNow : null,
        });
        await db.SaveChangesAsync();
    }

    // ── Cancel / resume / pending changes / card ─────────────────────────────

    public async Task<BillingActionResult> CancelAsync(int companyId)
    {
        EnsureConfigured();
        var current = await CurrentAsync(companyId);
        if (!IsRunningPaid(current)) throw new DomainException("Não há assinatura paga para cancelar.");

        await asaas.DeleteSubscriptionAsync(current!.AsaasSubscriptionId!);
        current.CanceledAt = DateTime.UtcNow;
        current.PendingPlanId = null;

        var paidAhead = current.Status == SubscriptionStatus.Active && current.CurrentPeriodEnd > DateTime.UtcNow;
        if (paidAhead) current.CancelAtPeriodEnd = true;
        else current.Status = SubscriptionStatus.Canceled;
        await db.SaveChangesAsync();

        var until = paidAhead ? current.CurrentPeriodEnd : null;
        await ops.NotifyOwnerAsync(companyId, (owner, company) =>
            email.SendSubscriptionCancelledAsync(owner.Email, owner.FirstName, company.Name, until));
        return new BillingActionResult(paidAhead ? "scheduled" : "applied", EffectiveAt: until);
    }

    public async Task<BillingActionResult> ResumeAsync(int companyId, int userId)
    {
        EnsureConfigured();
        var current = await CurrentAsync(companyId);
        if (current is null || !current.CancelAtPeriodEnd || current.Status != SubscriptionStatus.Active)
            throw new DomainException("Não há cancelamento para desfazer.");

        var company = await db.Companies.FindAsync(companyId) ?? throw new NotFoundException("Company not found.");
        var choice = new Choice(current.Plan, current.Cycle, current.PaymentMethod ?? BillingPaymentMethod.CreditCard,
            current.Price ?? BillingOperations.PriceFor(current.Plan, current.Cycle));
        var start = DateOnly.FromDateTime(current.CurrentPeriodEnd!.Value.Add(BillingMath.BrasiliaOffset));

        // A saved card (with tokenization) resumes without leaving the app.
        if (choice.Method == BillingPaymentMethod.CreditCard && asaas.TokenizationEnabled && !string.IsNullOrEmpty(current.CardToken))
        {
            var customerId = await ops.EnsureCustomerAsync(company);
            var checkout = new BillingCheckout
            {
                CompanyId = companyId, Kind = BillingCheckoutKind.Replacement, PlanId = choice.Plan.Id,
                Cycle = choice.Cycle, Method = choice.Method, Amount = choice.Price,
                ReplacesSubscriptionId = current.Id, StartDate = start, CreatedByUserId = userId,
            };
            db.BillingCheckouts.Add(checkout);
            await db.SaveChangesAsync();
            try
            {
                var created = await asaas.CreateSubscriptionAsync(new AsaasSubscriptionRequest(
                    customerId, "CREDIT_CARD", choice.Price, start.ToString("yyyy-MM-dd"),
                    BillingMath.ToAsaasCycle(choice.Cycle), BillingOperations.Description(choice.Plan, choice.Cycle),
                    settings.CheckoutReference(checkout.Id), CreditCardToken: current.CardToken));
                var next = await ops.LinkAsync(checkout, created.Id);
                // Nothing is due until the paid period ends: keep the card details and
                // let the next charge activate it like any renewal.
                next.CardToken = current.CardToken; next.CardBrand = current.CardBrand; next.CardLast4 = current.CardLast4;
                await db.SaveChangesAsync();
                return new BillingActionResult("applied", EffectiveAt: current.CurrentPeriodEnd);
            }
            catch (AsaasException ex)
            {
                logger.LogInformation("Saved-card resume failed for company {CompanyId}: {Code}", companyId, ex.Code);
                checkout.Status = BillingCheckoutStatus.Canceled;
                await db.SaveChangesAsync();
            }
        }

        return await StartSubscriptionAsync(company, userId, choice, BillingCheckoutKind.Replacement, current, start);
    }

    public async Task<BillingActionResult> CancelPendingChangeAsync(int companyId)
    {
        EnsureConfigured();
        var current = await CurrentAsync(companyId);
        if (current?.PendingPlanId is null) throw new DomainException("Não há troca de plano agendada.");

        var price = current.Price ?? BillingOperations.PriceFor(current.Plan, current.Cycle);
        await asaas.UpdateSubscriptionValueAsync(current.AsaasSubscriptionId!, price,
            BillingOperations.Description(current.Plan, current.Cycle));
        current.PendingPlanId = null;
        await db.SaveChangesAsync();
        return new BillingActionResult("applied");
    }

    public async Task<BillingActionResult> UpdateCardAsync(int companyId, int userId)
    {
        EnsureConfigured();
        var current = await CurrentAsync(companyId);
        if (!IsRunningPaid(current) || current!.PaymentMethod != BillingPaymentMethod.CreditCard)
            throw new DomainException("Não há assinatura no cartão para atualizar.");

        var company = await db.Companies.FindAsync(companyId) ?? throw new NotFoundException("Company not found.");
        var choice = new Choice(current.Plan, current.Cycle, BillingPaymentMethod.CreditCard,
            current.Price ?? BillingOperations.PriceFor(current.Plan, current.Cycle));

        // Paid up: the new card is first charged when the current period ends.
        // Overdue: the new card pays now and starts a fresh period (the overdue charge is dropped).
        DateOnly? start = NextStart(current) is { } at && current.Status == SubscriptionStatus.Active
            ? DateOnly.FromDateTime(at.Add(BillingMath.BrasiliaOffset))
            : null;
        return await StartSubscriptionAsync(company, userId, choice, BillingCheckoutKind.Replacement, current, start);
    }

    // ── Stopping (company deleted, admin assignment) ─────────────────────────

    public async Task StopAsaasSubscriptionsAsync(int companyId, CancellationToken ct = default)
    {
        var running = await db.Subscriptions
            .Where(s => s.CompanyId == companyId && s.AsaasSubscriptionId != null && s.Status != SubscriptionStatus.Canceled)
            .ToListAsync(ct);
        foreach (var sub in running)
        {
            try
            {
                await asaas.DeleteSubscriptionAsync(sub.AsaasSubscriptionId!, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not stop Asaas subscription {AsaasId} for company {CompanyId}",
                    sub.AsaasSubscriptionId, companyId);
                throw new DomainException("Não foi possível cancelar a cobrança na Asaas. Tente de novo.");
            }
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private string SuccessUrl(int checkoutId) => $"{settings.ReturnBaseUrl}/billing?checkout={checkoutId}&status=success";

    private AsaasCallback Callback(int checkoutId) => new(
        SuccessUrl(checkoutId),
        $"{settings.ReturnBaseUrl}/billing?checkout={checkoutId}&status=canceled",
        $"{settings.ReturnBaseUrl}/billing?checkout={checkoutId}&status=expired");

    private async Task CancelRemoteCheckoutAsync(BillingCheckout checkout)
    {
        try
        {
            if (!string.IsNullOrEmpty(checkout.AsaasCheckoutId))
                await asaas.CancelCheckoutAsync(checkout.AsaasCheckoutId);
            else if (checkout.AsaasSubscriptionId is { } subId
                     && await db.Subscriptions.FirstOrDefaultAsync(s => s.AsaasSubscriptionId == subId) is { Status: SubscriptionStatus.Incomplete } pending)
            {
                // An unpaid Pix subscription: drop it so its invoice can't be paid anymore.
                await asaas.DeleteSubscriptionAsync(subId);
                pending.Status = SubscriptionStatus.Canceled;
                pending.CanceledAt = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not cancel stale checkout {Id}", checkout.Id);
        }
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
