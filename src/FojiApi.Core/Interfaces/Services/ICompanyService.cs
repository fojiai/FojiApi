using FojiApi.Core.Enums;

namespace FojiApi.Core.Interfaces.Services;

public interface ICompanyService
{
    Task<CreateCompanyResult> CreateCompanyAsync(int userId, string name, string? slug, string? description);
    Task<CompanyDetailResult> GetCompanyAsync(int companyId);
    Task<CompanyDetailResult> UpdateCompanyAsync(int companyId, string? name, string? description);
    Task<IEnumerable<MemberResult>> GetMembersAsync(int companyId);
    Task RemoveMemberAsync(int companyId, int targetUserId, int requestingUserId);
    Task InviteMemberAsync(int companyId, int inviterUserId, string email, string role);
    Task DeleteCompanyAsync(int companyId, int requestingUserId);
}

public record CreateCompanyResult(int Id, string Name, string Slug, string NewToken);
public record CompanyDetailResult(
    int Id, string Name, string Slug, string? Description, string? LogoUrl,
    ActiveSubscriptionResult? Subscription
);
public record ActiveSubscriptionResult(
    string Status, string PlanName, int MaxAgents, bool HasWhatsApp,
    DateTime? CurrentPeriodEnd, DateTime? TrialEndsAt
);
public record MemberResult(int UserId, string Email, string FirstName, string LastName, string Role, DateTime JoinedAt);
