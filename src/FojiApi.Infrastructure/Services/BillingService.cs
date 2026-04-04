using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;

namespace FojiApi.Infrastructure.Services;

public class BillingService(FojiDbContext db, IConfiguration configuration, IEmailService emailService) : IBillingService
{
    private string SecretKey => configuration["Stripe:SecretKey"]
        ?? throw new InvalidOperationException("Stripe:SecretKey not configured");
    private string WebhookSecret => configuration["Stripe:WebhookSecret"]
        ?? throw new InvalidOperationException("Stripe:WebhookSecret not configured");
    private string AppBaseUrl => configuration["App:BaseUrl"] ?? "https://app.foji.ai";

    public async Task<string> CreateCheckoutSessionAsync(int companyId, int planId, int userId)
    {
        StripeConfiguration.ApiKey = SecretKey;

        var plan = await db.Plans.FindAsync(planId);
        if (plan == null || string.IsNullOrEmpty(plan.StripePriceId))
            throw new DomainException("Plan not found or not yet configured for billing.");

        var company = await db.Companies.FindAsync(companyId)
            ?? throw new NotFoundException("Company not found.");

        // Check for existing active subscription
        var existingSub = await db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.CompanyId == companyId &&
                        s.StripeSubscriptionId != null &&
                        (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        // If already subscribed with Stripe, redirect to the customer portal
        // so the user can review and confirm the plan change themselves.
        if (existingSub != null && !string.IsNullOrEmpty(existingSub.StripeSubscriptionId))
        {
            if (existingSub.PlanId == planId)
                throw new DomainException("You are already on this plan.");

            var portalSession = await new Stripe.BillingPortal.SessionService().CreateAsync(
                new Stripe.BillingPortal.SessionCreateOptions
                {
                    Customer = company.StripeCustomerId,
                    ReturnUrl = $"{AppBaseUrl}/billing",
                    FlowData = new Stripe.BillingPortal.SessionFlowDataOptions
                    {
                        Type = "subscription_update_confirm",
                        SubscriptionUpdateConfirm = new Stripe.BillingPortal.SessionFlowDataSubscriptionUpdateConfirmOptions
                        {
                            Subscription = existingSub.StripeSubscriptionId,
                            Items =
                            [
                                new Stripe.BillingPortal.SessionFlowDataSubscriptionUpdateConfirmItemOptions
                                {
                                    Id = (await new SubscriptionService().GetAsync(existingSub.StripeSubscriptionId!))
                                        .Items.Data.First().Id,
                                    Price = plan.StripePriceId,
                                    Quantity = 1,
                                }
                            ],
                        },
                    },
                });
            return portalSession.Url;
        }

        // No existing subscription — create a new checkout session
        var customerId = await EnsureStripeCustomerAsync(company, userId);

        var session = await new SessionService().CreateAsync(new SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            LineItems = [new SessionLineItemOptions { Price = plan.StripePriceId, Quantity = 1 }],
            SuccessUrl = $"{AppBaseUrl}/billing?session_id={{CHECKOUT_SESSION_ID}}&status=success",
            CancelUrl = $"{AppBaseUrl}/billing?status=canceled",
            SubscriptionData = plan.TrialDays > 0
                ? new SessionSubscriptionDataOptions { TrialPeriodDays = plan.TrialDays }
                : null,
            Metadata = new Dictionary<string, string>
            {
                ["companyId"] = companyId.ToString(),
                ["planId"] = planId.ToString()
            }
        });

        return session.Url;
    }

    public async Task<string> CreateCustomerPortalSessionAsync(int companyId)
    {
        StripeConfiguration.ApiKey = SecretKey;

        var company = await db.Companies.FindAsync(companyId)
            ?? throw new NotFoundException("Company not found.");

        if (string.IsNullOrEmpty(company.StripeCustomerId))
            throw new DomainException("No billing account found for this company.");

        var session = await new Stripe.BillingPortal.SessionService().CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = company.StripeCustomerId,
                ReturnUrl = $"{AppBaseUrl}/billing"
            });

        return session.Url;
    }

    public async Task<SubscriptionResult?> GetSubscriptionAsync(int companyId)
    {
        var sub = await db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        // Self-healing: if the company has a Stripe customer but no active Stripe subscription
        // locally, check Stripe for active subscriptions and sync them.
        var hasActiveStripeSub = sub != null
            && !string.IsNullOrEmpty(sub.StripeSubscriptionId)
            && sub.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing;

        if (!hasActiveStripeSub)
        {
            var company = await db.Companies.FindAsync(companyId);
            if (company != null && !string.IsNullOrEmpty(company.StripeCustomerId))
            {
                sub = await SyncSubscriptionFromStripeAsync(companyId, company.StripeCustomerId) ?? sub;
            }
        }

        if (sub == null) return null;

        return new SubscriptionResult(
            sub.Id,
            sub.Status.ToString().ToLower(),
            new SubscriptionPlanResult(sub.Plan.Id, sub.Plan.Name, sub.Plan.MaxAgents, sub.Plan.HasWhatsApp, sub.Plan.HasEscalationContacts, sub.Plan.MaxConversationsPerMonth, sub.Plan.MaxMessagesPerMonth),
            sub.CurrentPeriodStart, sub.CurrentPeriodEnd, sub.TrialEndsAt, sub.CanceledAt,
            !string.IsNullOrEmpty(sub.StripeSubscriptionId));
    }

    /// <summary>
    /// Check Stripe for active subscriptions for a customer and sync them locally.
    /// Returns the synced subscription if found, null otherwise.
    /// </summary>
    private async Task<Core.Entities.Subscription?> SyncSubscriptionFromStripeAsync(int companyId, string stripeCustomerId)
    {
        try
        {
            StripeConfiguration.ApiKey = SecretKey;
            var stripeSubs = await new SubscriptionService().ListAsync(new SubscriptionListOptions
            {
                Customer = stripeCustomerId,
                Status = "all",
                Limit = 5,
            });

            // Find the most recent active or trialing Stripe subscription
            var activeSub = stripeSubs.Data
                .Where(s => s.Status is "active" or "trialing")
                .OrderByDescending(s => s.Created)
                .FirstOrDefault();

            if (activeSub == null) return null;

            // Check if we already have this subscription locally
            var existing = await db.Subscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == activeSub.Id);

            if (existing != null)
            {
                // Update status in case it drifted
                existing.Status = MapStatus(activeSub.Status);
                existing.CurrentPeriodStart = activeSub.CurrentPeriodStart;
                existing.CurrentPeriodEnd = activeSub.CurrentPeriodEnd;
                existing.TrialEndsAt = activeSub.TrialEnd;
                existing.CanceledAt = activeSub.CanceledAt;
                await db.SaveChangesAsync();
                return existing;
            }

            // We don't have this subscription locally — create it.
            // Match the Stripe price to a local plan.
            var priceId = activeSub.Items.Data.FirstOrDefault()?.Price?.Id;
            var plan = !string.IsNullOrEmpty(priceId)
                ? await db.Plans.FirstOrDefaultAsync(p => p.StripePriceId == priceId)
                : null;

            // Also check metadata for planId (set by our checkout flow)
            if (plan == null && activeSub.Metadata.TryGetValue("planId", out var planIdStr)
                && int.TryParse(planIdStr, out var planId))
            {
                plan = await db.Plans.FindAsync(planId);
            }

            if (plan == null) return null; // Can't map to a local plan

            // Cancel any local-only trials
            var localTrials = await db.Subscriptions
                .Where(s => s.CompanyId == companyId
                    && s.StripeSubscriptionId == null
                    && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
                .ToListAsync();
            foreach (var trial in localTrials)
            {
                trial.Status = SubscriptionStatus.Canceled;
                trial.CanceledAt = DateTime.UtcNow;
            }

            var newSub = new Core.Entities.Subscription
            {
                CompanyId = companyId,
                PlanId = plan.Id,
                Status = MapStatus(activeSub.Status),
                StripeSubscriptionId = activeSub.Id,
                StripeCustomerId = stripeCustomerId,
                CurrentPeriodStart = activeSub.CurrentPeriodStart,
                CurrentPeriodEnd = activeSub.CurrentPeriodEnd,
                TrialEndsAt = activeSub.TrialEnd,
            };
            db.Subscriptions.Add(newSub);
            await db.SaveChangesAsync();

            // Reload with Plan navigation property
            await db.Entry(newSub).Reference(s => s.Plan).LoadAsync();
            return newSub;
        }
        catch
        {
            // Don't let Stripe API errors break the billing page
            return null;
        }
    }

    public async Task<SubscriptionResult?> VerifyCheckoutSessionAsync(int companyId, string sessionId)
    {
        StripeConfiguration.ApiKey = SecretKey;

        Session session;
        try
        {
            session = await new SessionService().GetAsync(sessionId);
        }
        catch
        {
            return await GetSubscriptionAsync(companyId);
        }

        // Only process if the session belongs to this company
        if (!session.Metadata.TryGetValue("companyId", out var cid) || cid != companyId.ToString())
            return await GetSubscriptionAsync(companyId);

        // If session is complete and has a subscription, ensure we have it locally
        if (session.Status == "complete" && !string.IsNullOrEmpty(session.SubscriptionId))
        {
            var existing = await db.Subscriptions
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == session.SubscriptionId);

            if (existing == null)
            {
                // Webhook hasn't arrived yet — process the checkout now
                await HandleCheckoutCompletedAsync(session);
            }
        }

        return await GetSubscriptionAsync(companyId);
    }

    public async Task HandleWebhookAsync(string payload, string signature)
    {
        StripeConfiguration.ApiKey = SecretKey;
        Event stripeEvent;

        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, WebhookSecret);
        }
        catch
        {
            throw new DomainException("Invalid webhook signature.");
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutCompletedAsync((Session)stripeEvent.Data.Object);
                break;
            case "customer.subscription.updated":
                await HandleSubscriptionUpdatedAsync((Stripe.Subscription)stripeEvent.Data.Object);
                break;
            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync((Stripe.Subscription)stripeEvent.Data.Object);
                break;
            case "invoice.payment_failed":
                await HandlePaymentFailedAsync((Invoice)stripeEvent.Data.Object);
                break;
        }
    }

    private async Task HandleCheckoutCompletedAsync(Session session)
    {
        if (!int.TryParse(session.Metadata.GetValueOrDefault("companyId"), out var companyId)) return;
        if (!int.TryParse(session.Metadata.GetValueOrDefault("planId"), out var planId)) return;

        var stripeSub = await new SubscriptionService().GetAsync(session.SubscriptionId);

        // Cancel any other active subscriptions for this company (prevent duplicates)
        var otherSubs = await db.Subscriptions
            .Where(s => s.CompanyId == companyId &&
                        s.StripeSubscriptionId != stripeSub.Id &&
                        (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .ToListAsync();

        foreach (var old in otherSubs)
        {
            old.Status = SubscriptionStatus.Canceled;
            old.CanceledAt = DateTime.UtcNow;
        }

        var existing = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.StripeSubscriptionId == stripeSub.Id);

        if (existing == null)
        {
            db.Subscriptions.Add(new Core.Entities.Subscription
            {
                CompanyId = companyId,
                PlanId = planId,
                Status = MapStatus(stripeSub.Status),
                StripeSubscriptionId = stripeSub.Id,
                StripeCustomerId = stripeSub.CustomerId,
                CurrentPeriodStart = stripeSub.CurrentPeriodStart,
                CurrentPeriodEnd = stripeSub.CurrentPeriodEnd,
                TrialEndsAt = stripeSub.TrialEnd
            });
        }
        else
        {
            existing.PlanId = planId;
            existing.Status = MapStatus(stripeSub.Status);
            existing.CurrentPeriodStart = stripeSub.CurrentPeriodStart;
            existing.CurrentPeriodEnd = stripeSub.CurrentPeriodEnd;
        }

        await db.SaveChangesAsync();
    }

    private async Task HandleSubscriptionUpdatedAsync(Stripe.Subscription stripeSub)
    {
        var sub = await db.Subscriptions.FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id);
        if (sub == null) return;

        sub.Status = MapStatus(stripeSub.Status);
        sub.CurrentPeriodStart = stripeSub.CurrentPeriodStart;
        sub.CurrentPeriodEnd = stripeSub.CurrentPeriodEnd;
        sub.CanceledAt = stripeSub.CanceledAt;

        // Sync PlanId if the price changed (plan switch via Stripe portal or API)
        var currentPriceId = stripeSub.Items.Data.FirstOrDefault()?.Price?.Id;
        if (!string.IsNullOrEmpty(currentPriceId))
        {
            var matchingPlan = await db.Plans.FirstOrDefaultAsync(p => p.StripePriceId == currentPriceId);
            if (matchingPlan != null && matchingPlan.Id != sub.PlanId)
            {
                sub.PlanId = matchingPlan.Id;
            }
        }

        // Also check metadata for planId (set by our SwitchPlanAsync)
        if (stripeSub.Metadata.TryGetValue("planId", out var planIdStr) && int.TryParse(planIdStr, out var planId))
        {
            if (planId != sub.PlanId)
                sub.PlanId = planId;
        }

        await db.SaveChangesAsync();
    }

    private async Task HandleSubscriptionDeletedAsync(Stripe.Subscription stripeSub)
    {
        var sub = await db.Subscriptions
            .Include(s => s.Company)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id);
        if (sub == null) return;
        sub.Status = SubscriptionStatus.Canceled;
        sub.CanceledAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Notify the company owner
        var owner = await GetCompanyOwnerAsync(sub.CompanyId);
        if (owner != null)
            await emailService.SendSubscriptionCancelledAsync(owner.Email, owner.FirstName, sub.Company.Name);
    }

    private async Task HandlePaymentFailedAsync(Invoice invoice)
    {
        var sub = await db.Subscriptions
            .Include(s => s.Company)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == invoice.SubscriptionId);
        if (sub == null) return;
        sub.Status = SubscriptionStatus.PastDue;
        await db.SaveChangesAsync();

        // Notify the company owner
        var owner = await GetCompanyOwnerAsync(sub.CompanyId);
        if (owner != null)
            await emailService.SendPaymentFailedAsync(owner.Email, owner.FirstName, sub.Company.Name);
    }

    private async Task<User?> GetCompanyOwnerAsync(int companyId)
    {
        return await db.UserCompanies
            .Include(uc => uc.User)
            .Where(uc => uc.CompanyId == companyId && uc.Role == CompanyRole.Owner && uc.IsActive)
            .Select(uc => uc.User)
            .FirstOrDefaultAsync();
    }

    private async Task<string> EnsureStripeCustomerAsync(Company company, int userId)
    {
        if (!string.IsNullOrEmpty(company.StripeCustomerId))
            return company.StripeCustomerId;

        var user = await db.Users.FindAsync(userId);
        var customer = await new CustomerService().CreateAsync(new CustomerCreateOptions
        {
            Email = user!.Email,
            Name = company.Name,
            Metadata = new Dictionary<string, string> { ["companyId"] = company.Id.ToString() }
        });

        company.StripeCustomerId = customer.Id;
        await db.SaveChangesAsync();
        return customer.Id;
    }

    private static SubscriptionStatus MapStatus(string status) => status switch
    {
        "active" => SubscriptionStatus.Active,
        "trialing" => SubscriptionStatus.Trialing,
        "past_due" => SubscriptionStatus.PastDue,
        "canceled" or "cancelled" => SubscriptionStatus.Canceled,
        "unpaid" => SubscriptionStatus.Unpaid,
        _ => SubscriptionStatus.Active
    };
}
