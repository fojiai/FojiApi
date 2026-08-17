namespace FojiApi.Core.Interfaces.Services;

public interface IPlanService
{
    /// <summary>Returns active public plans — used by the public pricing page and onboarding.</summary>
    Task<IEnumerable<PlanResult>> GetActivePlansAsync();

    /// <summary>Returns ALL plans including inactive and private — admin only.</summary>
    Task<IEnumerable<PlanResult>> GetAllPlansAsync();

    /// <summary>Admin: create a new plan.</summary>
    Task<PlanResult> CreatePlanAsync(UpsertPlanRequest req);

    /// <summary>Admin: update an existing plan.</summary>
    Task<PlanResult> UpdatePlanAsync(int id, UpsertPlanRequest req);

    /// <summary>Admin: soft-delete (deactivate) a plan. Throws if plan has active subscriptions.</summary>
    Task DeletePlanAsync(int id);
}

public record UpsertPlanRequest(
    string Name,
    string Slug,
    string? Description,
    decimal MonthlyPrice,
    string Currency,
    string? StripePriceId,
    int MaxAgents,
    int MaxMembers,
    bool HasWhatsApp,
    bool HasEscalationContacts,
    bool HasGoogleCalendar,
    bool HasCrm,
    int MaxConversationsPerMonth,
    int MaxMessagesPerMonth,
    int TrialDays,
    bool IsActive,
    bool IsPublic,
    int? CustomForCompanyId,
    /// <summary>Outbound WhatsApp messages included. -1 uncapped, 0 none.</summary>
    int WhatsAppMessagesPerMonth = 0,
    /// <summary>Centavos per message past the allowance. 0 stops instead of billing.</summary>
    int WhatsAppOverageCentavos = 0,
    /// <summary>Marketing templates cost ~9x utility and are off unless deliberately enabled.</summary>
    bool WhatsAppAllowMarketing = false);

public record PlanResult(
    int Id,
    string Name,
    string Slug,
    string? Description,
    decimal MonthlyPrice,
    string Currency,
    string? StripePriceId,
    int MaxAgents,
    int MaxMembers,
    bool HasWhatsApp,
    bool HasEscalationContacts,
    bool HasGoogleCalendar,
    bool HasCrm,
    int MaxConversationsPerMonth,
    int MaxMessagesPerMonth,
    int TrialDays,
    bool IsPublic,
    bool IsActive,
    int? CustomForCompanyId,
    int WhatsAppMessagesPerMonth,
    int WhatsAppOverageCentavos,
    bool WhatsAppAllowMarketing);
