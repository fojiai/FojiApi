namespace FojiApi.Core.Interfaces.Services;

public interface IBillingService
{
    Task<string> CreateCheckoutSessionAsync(int companyId, int planId, int userId);
    Task<string> CreateCustomerPortalSessionAsync(int companyId);
    Task<SubscriptionResult?> GetSubscriptionAsync(int companyId);
    Task<SubscriptionResult?> VerifyCheckoutSessionAsync(int companyId, string sessionId);
    Task HandleWebhookAsync(string payload, string signature);
}

public record SubscriptionResult(
    int Id,
    string Status,
    SubscriptionPlanResult Plan,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    DateTime? TrialEndsAt,
    DateTime? CanceledAt,
    bool HasStripeSubscription
);

public record SubscriptionPlanResult(int Id, string Name, int MaxAgents, bool HasWhatsApp, bool HasEscalationContacts, int MaxConversationsPerMonth, int MaxMessagesPerMonth);
