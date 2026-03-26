using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Services;

public class AdminCompanyService(
    FojiDbContext db,
    ILogger<AdminCompanyService> logger
) : IAdminCompanyService
{
    // ── List ─────────────────────────────────────────────────────────────────

    public async Task<(IEnumerable<AdminCompanyListItem> Items, int TotalCount)> ListAsync(
        string? search, int page, int pageSize)
    {
        var query = db.Companies
            .Include(c => c.UserCompanies).ThenInclude(uc => uc.User)
            .Include(c => c.Subscriptions).ThenInclude(s => s.Plan)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(q) ||
                (c.TradeName != null && c.TradeName.ToLower().Contains(q)) ||
                (c.CpfCnpj != null && c.CpfCnpj.Contains(q)) ||
                c.Slug.ToLower().Contains(q) ||
                c.UserCompanies.Any(uc => uc.Role == CompanyRole.Owner && uc.User.Email.ToLower().Contains(q))
            );
        }

        var total = await query.CountAsync();

        var companies = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = companies.Select(c =>
        {
            var owner = c.UserCompanies
                .FirstOrDefault(uc => uc.Role == CompanyRole.Owner)?.User;

            var activeSub = c.Subscriptions
                .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();

            return new AdminCompanyListItem(
                Id: c.Id,
                Name: c.Name,
                TradeName: c.TradeName,
                Slug: c.Slug,
                AccountType: c.AccountType,
                CpfCnpj: c.CpfCnpj,
                OwnerEmail: owner?.Email ?? "—",
                CurrentPlanName: activeSub?.Plan?.Name,
                SubscriptionStatus: activeSub?.Status.ToString(),
                HasActiveSubscription: activeSub != null,
                CreatedAt: c.CreatedAt
            );
        });

        return (items, total);
    }

    // ── Get ──────────────────────────────────────────────────────────────────

    public async Task<AdminCompanyDetail> GetAsync(int companyId)
    {
        var company = await db.Companies
            .Include(c => c.UserCompanies).ThenInclude(uc => uc.User)
            .Include(c => c.Agents)
            .Include(c => c.Subscriptions).ThenInclude(s => s.Plan)
            .Include(c => c.Subscriptions).ThenInclude(s => s.AssignedByAdmin)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId)
            ?? throw new KeyNotFoundException($"Company {companyId} not found");

        var activeSub = company.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        return new AdminCompanyDetail(
            Id: company.Id,
            Name: company.Name,
            TradeName: company.TradeName,
            Slug: company.Slug,
            Description: company.Description,
            AccountType: company.AccountType,
            CpfCnpj: company.CpfCnpj,
            AdminNotes: company.AdminNotes,
            StripeCustomerId: company.StripeCustomerId,
            MemberCount: company.UserCompanies.Count,
            AgentCount: company.Agents.Count,
            CurrentPlanId: activeSub?.PlanId,
            CurrentPlanName: activeSub?.Plan?.Name,
            CurrentPlanIsPublic: activeSub?.Plan?.IsPublic,
            SubscriptionStatus: activeSub?.Status.ToString(),
            SubscriptionPeriodEnd: activeSub?.CurrentPeriodEnd,
            SubscriptionAssignedByAdminId: activeSub?.AssignedByAdminId,
            SubscriptionAdminNotes: activeSub?.AdminNotes,
            CreatedAt: company.CreatedAt
        );
    }

    // ── Assign Plan ───────────────────────────────────────────────────────────

    public async Task AssignPlanAsync(int companyId, AssignPlanRequest request, int adminUserId)
    {
        var company = await db.Companies.FindAsync(companyId)
            ?? throw new KeyNotFoundException($"Company {companyId} not found");

        var plan = await db.Plans.FindAsync(request.PlanId)
            ?? throw new KeyNotFoundException($"Plan {request.PlanId} not found");

        if (!plan.IsActive)
            throw new InvalidOperationException($"Plan '{plan.Name}' is not active.");

        // If this plan is locked to a specific company, enforce it.
        if (plan.CustomForCompanyId.HasValue && plan.CustomForCompanyId != companyId)
            throw new InvalidOperationException(
                $"Plan '{plan.Name}' is a custom plan for a different company.");

        // Deactivate existing active subscriptions
        var existing = await db.Subscriptions
            .Where(s => s.CompanyId == companyId &&
                        (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .ToListAsync();

        foreach (var sub in existing)
        {
            sub.Status = SubscriptionStatus.Canceled;
            sub.CanceledAt = DateTime.UtcNow;
        }

        // Create the new admin-assigned subscription
        var now = DateTime.UtcNow;
        var newSub = new Subscription
        {
            CompanyId = companyId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = request.PeriodEnd,
            AssignedByAdminId = adminUserId,
            AdminNotes = request.AdminNotes,
            // No Stripe IDs — this is a manual assignment
            StripeSubscriptionId = null,
            StripeCustomerId = company.StripeCustomerId
        };

        db.Subscriptions.Add(newSub);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Admin {AdminId} assigned plan '{PlanName}' (id={PlanId}) to company {CompanyId}. Notes: {Notes}",
            adminUserId, plan.Name, plan.Id, companyId, request.AdminNotes ?? "none");
    }

    // ── Remove Plan ───────────────────────────────────────────────────────────

    public async Task RemovePlanAsync(int companyId, int adminUserId)
    {
        var existing = await db.Subscriptions
            .Where(s => s.CompanyId == companyId &&
                        (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .ToListAsync();

        if (!existing.Any())
            throw new InvalidOperationException("Company has no active subscription to remove.");

        foreach (var sub in existing)
        {
            sub.Status = SubscriptionStatus.Canceled;
            sub.CanceledAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Admin {AdminId} removed plan from company {CompanyId}.", adminUserId, companyId);
    }

    // ── Create Company ────────────────────────────────────────────────────────

    public async Task<int> CreateCompanyAsync(CreateAdminCompanyRequest request, int adminUserId)
    {
        // Validate slug uniqueness
        if (await db.Companies.AnyAsync(c => c.Slug == request.Slug))
            throw new InvalidOperationException($"Slug '{request.Slug}' is already taken.");

        // Resolve or create owner user
        var owner = await db.Users.FirstOrDefaultAsync(u => u.Email == request.OwnerEmail);
        bool isNewUser = owner == null;

        if (owner == null)
        {
            if (string.IsNullOrWhiteSpace(request.OwnerFirstName) || string.IsNullOrWhiteSpace(request.OwnerLastName))
                throw new InvalidOperationException(
                    "OwnerFirstName and OwnerLastName are required when creating a new user account.");

            // Create a placeholder user — they'll set their password via invite flow
            owner = new User
            {
                Email = request.OwnerEmail,
                FirstName = request.OwnerFirstName!,
                LastName = request.OwnerLastName!,
                // Random unusable hash — will be replaced on invite accept
                HashedPassword = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                // Pre-verify since admin is creating this on behalf of the person
                EmailVerifiedAt = DateTime.UtcNow
            };
            db.Users.Add(owner);
            await db.SaveChangesAsync(); // get Id
        }

        // Validate email domain collision for existing users already owning a company with this slug
        var company = new Company
        {
            Name = request.Name,
            TradeName = request.TradeName,
            Slug = request.Slug,
            Description = request.Description,
            AccountType = request.AccountType,
            CpfCnpj = request.CpfCnpj,
            AdminNotes = request.AdminNotes
        };

        db.Companies.Add(company);
        await db.SaveChangesAsync(); // get Id

        db.UserCompanies.Add(new UserCompany
        {
            UserId = owner.Id,
            CompanyId = company.Id,
            Role = CompanyRole.Owner
        });

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Admin {AdminId} created company '{Slug}' (id={CompanyId}), AccountType={Type}, isNewUser={New}",
            adminUserId, request.Slug, company.Id, request.AccountType, isNewUser);

        return company.Id;
    }

    // ── Update Notes ──────────────────────────────────────────────────────────

    public async Task UpdateNotesAsync(int companyId, string? notes)
    {
        var company = await db.Companies.FindAsync(companyId)
            ?? throw new KeyNotFoundException($"Company {companyId} not found");

        company.AdminNotes = notes;
        await db.SaveChangesAsync();
    }

    // ── Platform Stats ────────────────────────────────────────────────────────

    public async Task<PlatformStats> GetPlatformStatsAsync()
    {
        var totalCompanies = await db.Companies.CountAsync();
        var totalUsers = await db.Users.CountAsync();
        var totalAgents = await db.Agents.CountAsync();

        var activeSubscriptions = await db.Subscriptions
            .CountAsync(s => s.Status == SubscriptionStatus.Active);

        var trialingSubscriptions = await db.Subscriptions
            .CountAsync(s => s.Status == SubscriptionStatus.Trialing);

        // Companies that have never had a subscription or only have cancelled ones
        var companiesWithActiveSub = await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing)
            .Select(s => s.CompanyId)
            .Distinct()
            .CountAsync();

        var companiesWithNoSubscription = totalCompanies - companiesWithActiveSub;

        return new PlatformStats(
            totalCompanies,
            totalUsers,
            totalAgents,
            activeSubscriptions,
            trialingSubscriptions,
            companiesWithNoSubscription
        );
    }
}
