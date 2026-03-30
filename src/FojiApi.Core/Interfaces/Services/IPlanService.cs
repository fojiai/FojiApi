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
    bool HasWhatsApp,
    bool HasEscalationContacts,
    int MaxConversationsPerMonth,
    int MaxMessagesPerMonth,
    int TrialDays,
    bool IsActive,
    bool IsPublic,
    int? CustomForCompanyId);

public record PlanResult(
    int Id,
    string Name,
    string Slug,
    string? Description,
    decimal MonthlyPrice,
    string Currency,
    int MaxAgents,
    bool HasWhatsApp,
    bool HasEscalationContacts,
    int MaxConversationsPerMonth,
    int MaxMessagesPerMonth,
    int TrialDays,
    bool IsPublic,
    bool IsActive,
    int? CustomForCompanyId);
