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

        if (sub == null) return null;

        return new SubscriptionResult(
            sub.Id,
            sub.Status.ToString().ToLower(),
            new SubscriptionPlanResult(sub.Plan.Id, sub.Plan.Name, sub.Plan.MaxAgents, sub.Plan.HasWhatsApp, sub.Plan.HasEscalationContacts, sub.Plan.MaxConversationsPerMonth, sub.Plan.MaxMessagesPerMonth),
            sub.CurrentPeriodStart, sub.CurrentPeriodEnd, sub.TrialEndsAt, sub.CanceledAt);
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
