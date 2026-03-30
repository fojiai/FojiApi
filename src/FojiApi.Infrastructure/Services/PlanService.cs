using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class PlanService(FojiDbContext db) : IPlanService
{
    public async Task<IEnumerable<PlanResult>> GetActivePlansAsync()
    {
        return await db.Plans
            .Where(p => p.IsActive && p.IsPublic)
            .OrderBy(p => p.MonthlyPrice)
            .Select(p => ToResult(p))
            .ToListAsync();
    }

    public async Task<IEnumerable<PlanResult>> GetAllPlansAsync()
    {
        return await db.Plans
            .OrderBy(p => p.IsPublic ? 0 : 1)
            .ThenBy(p => p.MonthlyPrice)
            .Select(p => ToResult(p))
            .ToListAsync();
    }

    public async Task<PlanResult> CreatePlanAsync(UpsertPlanRequest req)
    {
        var plan = new Plan
        {
            Name = req.Name,
            Slug = req.Slug,
            Description = req.Description,
            MonthlyPrice = req.MonthlyPrice,
            Currency = req.Currency ?? "USD",
            StripePriceId = req.StripePriceId,
            MaxAgents = req.MaxAgents,
            MaxMembers = req.MaxMembers,
            HasWhatsApp = req.HasWhatsApp,
            HasEscalationContacts = req.HasEscalationContacts,
            MaxConversationsPerMonth = req.MaxConversationsPerMonth,
            MaxMessagesPerMonth = req.MaxMessagesPerMonth,
            TrialDays = req.TrialDays,
            IsActive = req.IsActive,
            IsPublic = req.IsPublic,
            CustomForCompanyId = req.CustomForCompanyId,
        };
        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        return ToResult(plan);
    }

    public async Task<PlanResult> UpdatePlanAsync(int id, UpsertPlanRequest req)
    {
        var plan = await db.Plans.FindAsync(id)
            ?? throw new NotFoundException($"Plan {id} not found.");

        plan.Name = req.Name;
        plan.Slug = req.Slug;
        plan.Description = req.Description;
        plan.MonthlyPrice = req.MonthlyPrice;
        plan.Currency = req.Currency ?? plan.Currency;
        plan.StripePriceId = req.StripePriceId;
        plan.MaxAgents = req.MaxAgents;
        plan.MaxMembers = req.MaxMembers;
        plan.HasWhatsApp = req.HasWhatsApp;
        plan.HasEscalationContacts = req.HasEscalationContacts;
        plan.MaxConversationsPerMonth = req.MaxConversationsPerMonth;
        plan.MaxMessagesPerMonth = req.MaxMessagesPerMonth;
        plan.TrialDays = req.TrialDays;
        plan.IsActive = req.IsActive;
        plan.IsPublic = req.IsPublic;
        plan.CustomForCompanyId = req.CustomForCompanyId;

        await db.SaveChangesAsync();
        return ToResult(plan);
    }

    public async Task DeletePlanAsync(int id)
    {
        var plan = await db.Plans.FindAsync(id)
            ?? throw new NotFoundException($"Plan {id} not found.");

        var hasActive = await db.Subscriptions
            .AnyAsync(s => s.PlanId == id &&
                          (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing));

        if (hasActive)
            throw new InvalidOperationException("Cannot delete a plan that has active subscriptions.");

        // Soft-delete: just deactivate
        plan.IsActive = false;
        await db.SaveChangesAsync();
    }

    private static PlanResult ToResult(Plan p)
        => new(p.Id, p.Name, p.Slug, p.Description, p.MonthlyPrice, p.Currency, p.MaxAgents, p.MaxMembers,
               p.HasWhatsApp, p.HasEscalationContacts, p.MaxConversationsPerMonth, p.MaxMessagesPerMonth,
               p.TrialDays, p.IsPublic, p.IsActive, p.CustomForCompanyId);
}
