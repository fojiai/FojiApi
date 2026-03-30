using FojiApi.Core.Enums;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class PlanEnforcementService(FojiDbContext db) : IPlanEnforcementService
{
    private const long MaxFileSizeBytes = 30L * 1024 * 1024; // 30 MB

    public async Task EnsureCanCreateAgentAsync(int companyId)
    {
        var plan = await GetActivePlanAsync(companyId);
        if (plan == null)
            throw new InvalidOperationException("No active subscription found. Please subscribe to a plan to create agents.");

        var activeAgentCount = await db.Agents
            .CountAsync(a => a.CompanyId == companyId && a.IsActive);

        if (activeAgentCount >= plan.MaxAgents)
            throw new InvalidOperationException(
                $"Your {plan.Name} plan allows up to {plan.MaxAgents} active agent(s). " +
                "Please upgrade your plan or deactivate an existing agent.");
    }

    public async Task EnsureCanEnableWhatsAppAsync(int companyId)
    {
        var plan = await GetActivePlanAsync(companyId);
        if (plan == null || !plan.HasWhatsApp)
            throw new InvalidOperationException(
                "WhatsApp integration is only available on the Scale plan. Please upgrade to enable this feature.");
    }

    public async Task EnsureCanUseEscalationContactsAsync(int companyId)
    {
        var plan = await GetActivePlanAsync(companyId);
        if (plan == null || !plan.HasEscalationContacts)
            throw new InvalidOperationException(
                "Escalation contacts are available on the Professional and Scale plans. Please upgrade to enable this feature.");
    }

    public async Task EnsureCanInviteMemberAsync(int companyId)
    {
        var plan = await GetActivePlanAsync(companyId);
        if (plan == null)
            throw new InvalidOperationException("No active subscription found. Please subscribe to a plan to invite members.");

        if (plan.MaxMembers > 0)
        {
            var memberCount = await db.UserCompanies
                .CountAsync(uc => uc.CompanyId == companyId && uc.IsActive);
            var pendingInvites = await db.Invitations
                .CountAsync(i => i.CompanyId == companyId && i.AcceptedAt == null && i.ExpiresAt > DateTime.UtcNow);

            if (memberCount + pendingInvites >= plan.MaxMembers)
                throw new InvalidOperationException(
                    $"Your {plan.Name} plan allows up to {plan.MaxMembers} team member(s). " +
                    "Please upgrade your plan or remove an existing member.");
        }
    }

    public async Task EnsureHasActiveSubscriptionAsync(int companyId)
    {
        var subscription = await db.Subscriptions
            .Where(s => s.CompanyId == companyId &&
                        (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .FirstOrDefaultAsync();

        if (subscription == null)
            throw new InvalidOperationException("No active subscription found. Please subscribe to a plan to continue.");
    }

    public void EnsureFileSizeAllowed(long fileSizeBytes)
    {
        if (fileSizeBytes > MaxFileSizeBytes)
            throw new InvalidOperationException(
                $"File size exceeds the maximum allowed size of 30 MB. Your file is {fileSizeBytes / 1024 / 1024} MB.");
    }

    private async Task<Core.Entities.Plan?> GetActivePlanAsync(int companyId)
    {
        var subscription = await db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.CompanyId == companyId &&
                        (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        return subscription?.Plan;
    }
}
