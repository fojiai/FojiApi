using FojiApi.Core.Enums;

namespace FojiApi.Core.Interfaces.Services;

public record AdminCompanyListItem(
    int Id,
    string Name,
    string? TradeName,
    string Slug,
    AccountType AccountType,
    string? CpfCnpj,
    string OwnerEmail,
    string? CurrentPlanName,
    string? SubscriptionStatus,
    bool HasActiveSubscription,
    DateTime CreatedAt
);

public record AdminCompanyDetail(
    int Id,
    string Name,
    string? TradeName,
    string Slug,
    string? Description,
    AccountType AccountType,
    string? CpfCnpj,
    string? AdminNotes,
    string? StripeCustomerId,
    int MemberCount,
    int AgentCount,
    // Current subscription
    int? CurrentPlanId,
    string? CurrentPlanName,
    bool? CurrentPlanIsPublic,
    string? SubscriptionStatus,
    DateTime? SubscriptionPeriodEnd,
    int? SubscriptionAssignedByAdminId,
    string? SubscriptionAdminNotes,
    DateTime CreatedAt
);

public record AssignPlanRequest(
    int PlanId,
    string? AdminNotes,
    DateTime? PeriodEnd   // null = no expiry (manual billing)
);

public record CreateAdminCompanyRequest(
    string Name,
    string? TradeName,
    string Slug,
    AccountType AccountType,
    string? CpfCnpj,
    string? Description,
    string? AdminNotes,
    // Owner account — creates user if email does not exist
    string OwnerEmail,
    string? OwnerFirstName,
    string? OwnerLastName
);

public record PlatformStats(
    int TotalCompanies,
    int TotalUsers,
    int TotalAgents,
    int ActiveSubscriptions,
    int TrialingSubscriptions,
    int CompaniesWithNoSubscription
);

public interface IAdminCompanyService
{
    Task<(IEnumerable<AdminCompanyListItem> Items, int TotalCount)> ListAsync(
        string? search, int page, int pageSize);

    Task<AdminCompanyDetail> GetAsync(int companyId);

    /// <summary>
    /// Assign any plan (including private custom plans) directly to a company,
    /// bypassing Stripe. Creates or replaces the active subscription.
    /// </summary>
    Task AssignPlanAsync(int companyId, AssignPlanRequest request, int adminUserId);

    /// <summary>
    /// Remove the current subscription (revert to no plan).
    /// </summary>
    Task RemovePlanAsync(int companyId, int adminUserId);

    /// <summary>
    /// Admin-creates a company (with optional Pessoa Física support) and
    /// seeds an owner user if OwnerEmail does not already exist.
    /// Returns the new company Id.
    /// </summary>
    Task<int> CreateCompanyAsync(CreateAdminCompanyRequest request, int adminUserId);

    /// <summary>Update admin notes on a company without touching anything else.</summary>
    Task UpdateNotesAsync(int companyId, string? notes);

    /// <summary>Aggregate platform-wide counts for the admin home page.</summary>
    Task<PlatformStats> GetPlatformStatsAsync();
}
