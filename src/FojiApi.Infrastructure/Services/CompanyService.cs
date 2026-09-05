using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Services;

public class CompanyService(
    FojiDbContext db,
    IJwtService jwtService,
    IEmailService emailService,
    IStorageService storage,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<CompanyService> logger) : ICompanyService
{
    /// <summary>How many companies one user may own via self-serve signup.</summary>
    private const int MaxSelfServeCompaniesPerUser = 3;


    public async Task<IEnumerable<UserCompanyResult>> GetUserCompaniesAsync(int userId)
    {
        return await db.UserCompanies
            .Include(uc => uc.Company)
            .Where(uc => uc.UserId == userId && uc.IsActive)
            .OrderBy(uc => uc.JoinedAt)
            .Select(uc => new UserCompanyResult(uc.CompanyId, uc.Company.Name, uc.Company.Slug, uc.Role.ToString().ToLower()))
            .ToListAsync();
    }

    public async Task<bool> IsSlugAvailableAsync(string slug)
    {
        var normalized = slug.ToLower().Trim();
        return !await db.Companies.AnyAsync(c => c.Slug == normalized);
    }

    public async Task<CreateCompanyResult> CreateCompanyAsync(int userId, string name, string? slug, string? description)
    {
        var resolvedSlug = System.Text.RegularExpressions.Regex
            .Replace((slug?.ToLower().Trim() ?? name.ToLower().Trim()), @"[^a-z0-9\-]", "-")
            .Trim('-');

        if (await db.Companies.AnyAsync(c => c.Slug == resolvedSlug))
            throw new ConflictException("A company with this slug already exists. Please choose a different one.");

        // Load the user first so EF can resolve the FK navigation on UserCompany
        var user = await db.Users.Include(u => u.UserCompanies).ThenInclude(uc => uc.Company).FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new NotFoundException($"User with id {userId} not found.");

        // Cap self-serve company creation. Creating a company auto-grants a trial
        // (below), so an uncapped endpoint let one account mint unlimited tenants,
        // each with a fresh trial and a fresh set of plan allowances.
        if (!user.IsSuperAdmin)
        {
            var ownedCount = await db.UserCompanies
                .CountAsync(uc => uc.UserId == userId && uc.IsActive && uc.Role == CompanyRole.Owner);
            if (ownedCount >= MaxSelfServeCompaniesPerUser)
                throw new DomainException(
                    $"You can create up to {MaxSelfServeCompaniesPerUser} companies. Contact support if you need more.");
        }

        var company = new Company { Name = name.Trim(), Slug = resolvedSlug, Description = description?.Trim() };
        db.Companies.Add(company);

        var userCompany = new UserCompany
        {
            User = user,
            Company = company,
            Role = CompanyRole.Owner,
            JoinedAt = DateTime.UtcNow
        };
        db.UserCompanies.Add(userCompany);

        // Create a trial subscription defaulting to the cheapest public plan.
        // Only the user's first company gets one — otherwise creating companies is
        // an unlimited trial dispenser.
        var alreadyHadTrial = await db.Subscriptions
            .AnyAsync(s => db.UserCompanies
                .Any(uc => uc.UserId == userId && uc.CompanyId == s.CompanyId && uc.Role == CompanyRole.Owner));

        var basePlan = alreadyHadTrial
            ? null
            : await db.Plans
                .Where(p => p.IsActive && p.IsPublic && p.CustomForCompanyId == null)
                .OrderBy(p => p.MonthlyPrice)
                .FirstOrDefaultAsync();

        if (basePlan != null)
        {
            var trialDays = basePlan.TrialDays > 0 ? basePlan.TrialDays : 15;
            db.Subscriptions.Add(new Subscription
            {
                Company = company,
                PlanId = basePlan.Id,
                Status = SubscriptionStatus.Trialing,
                CurrentPeriodStart = DateTime.UtcNow,
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(trialDays),
                TrialEndsAt = DateTime.UtcNow.AddDays(trialDays)
            });
        }

        await db.SaveChangesAsync();

        var newToken = jwtService.GenerateToken(user, user.UserCompanies.Where(uc => uc.IsActive));

        return new CreateCompanyResult(company.Id, company.Name, company.Slug, newToken);
    }

    public async Task<CompanyDetailResult> GetCompanyAsync(int companyId)
    {
        var company = await db.Companies
            .Include(c => c.Subscriptions).ThenInclude(s => s.Plan)
            .FirstOrDefaultAsync(c => c.Id == companyId)
            ?? throw new NotFoundException("Company not found.");

        var activeSub = company.Subscriptions
            .Where(s => s.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        return new CompanyDetailResult(
            company.Id, company.Name, company.Slug, company.Description, company.LogoUrl,
            activeSub == null ? null : new ActiveSubscriptionResult(
                activeSub.Status.ToString().ToLower(), activeSub.Plan.Name,
                activeSub.Plan.MaxAgents, activeSub.Plan.HasWhatsApp,
                activeSub.CurrentPeriodEnd, activeSub.TrialEndsAt)
        );
    }

    public async Task<CompanyDetailResult> UpdateCompanyAsync(int companyId, string? name, string? description)
    {
        var company = await db.Companies.FindAsync(companyId)
            ?? throw new NotFoundException("Company not found.");

        if (name != null) company.Name = name.Trim();
        if (description != null) company.Description = description.Trim();
        await db.SaveChangesAsync();

        return await GetCompanyAsync(companyId);
    }

    public async Task<IEnumerable<MemberResult>> GetMembersAsync(int companyId)
    {
        return await db.UserCompanies
            .Include(uc => uc.User)
            .Where(uc => uc.CompanyId == companyId && uc.IsActive)
            .Select(uc => new MemberResult(
                uc.UserId, uc.User.Email, uc.User.FirstName, uc.User.LastName,
                uc.Role.ToString().ToLower(), uc.JoinedAt))
            .ToListAsync();
    }

    public async Task RemoveMemberAsync(int companyId, int targetUserId, int requestingUserId)
    {
        var membership = await db.UserCompanies.FindAsync(targetUserId, companyId)
            ?? throw new NotFoundException("Member not found.");

        if (membership.Role == CompanyRole.Owner)
            throw new DomainException("Cannot remove the company owner.");

        if (targetUserId == requestingUserId)
            throw new DomainException("You cannot remove yourself. Transfer ownership first.");

        db.UserCompanies.Remove(membership);
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<InvitationResult>> GetInvitationsAsync(int companyId)
    {
        return await db.Invitations
            .Where(i => i.CompanyId == companyId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationResult(i.Id, i.Email, i.Role.ToString().ToLower(), i.ExpiresAt, i.AcceptedAt))
            .ToListAsync();
    }

    public async Task RevokeInvitationAsync(int companyId, int invitationId)
    {
        var invitation = await db.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.CompanyId == companyId)
            ?? throw new NotFoundException("Invitation not found.");

        if (invitation.AcceptedAt != null)
            throw new DomainException("Cannot revoke an accepted invitation.");

        db.Invitations.Remove(invitation);
        await db.SaveChangesAsync();
    }

    public async Task InviteMemberAsync(int companyId, int inviterUserId, string email, string role)
    {
        var company = await db.Companies.FindAsync(companyId)
            ?? throw new NotFoundException("Company not found.");

        var normalizedEmail = email.ToLower().Trim();

        var existingMember = await db.UserCompanies
            .Include(uc => uc.User)
            .FirstOrDefaultAsync(uc => uc.CompanyId == companyId && uc.User.Email == normalizedEmail);

        if (existingMember != null)
            throw new ConflictException("This user is already a member of the company.");

        if (!Enum.TryParse<CompanyRole>(role, true, out var parsedRole) || parsedRole == CompanyRole.Owner)
            throw new DomainException("Invalid role. Use 'admin' or 'user'.");

        // Cancel any pending invite for same email/company
        var staleInvite = await db.Invitations
            .FirstOrDefaultAsync(i => i.CompanyId == companyId && i.Email == normalizedEmail && i.AcceptedAt == null);
        if (staleInvite != null) db.Invitations.Remove(staleInvite);

        var invitation = new Invitation
        {
            CompanyId = companyId,
            InviterUserId = inviterUserId,
            Email = normalizedEmail,
            Role = parsedRole,
            Token = Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();

        var inviter = await db.Users.FindAsync(inviterUserId);
        await emailService.SendInvitationAsync(
            normalizedEmail, company.Name,
            $"{inviter!.FirstName} {inviter.LastName}",
            invitation.Token, parsedRole.ToString().ToLower());
    }

    public async Task DeleteCompanyAsync(int companyId, int requestingUserId)
    {
        var company = await db.Companies.FindAsync(companyId)
            ?? throw new NotFoundException($"Company {companyId} not found.");

        // Verify the requesting user is the owner
        var membership = await db.UserCompanies
            .FirstOrDefaultAsync(uc => uc.CompanyId == companyId && uc.UserId == requestingUserId);

        if (membership == null || membership.Role != CompanyRole.Owner)
            throw new ForbiddenException("Only the company owner can delete the company.");

        // Agent ids are needed to purge their S3 files, and they cascade away
        // with the company — so capture them before anything is deleted.
        var agentIds = await db.Agents
            .Where(a => a.CompanyId == companyId)
            .Select(a => a.Id)
            .ToListAsync();

        // Cancel active subscriptions first. (The Stripe webhook re-syncs, but we
        // also cancel in the DB immediately so nothing keeps billing.)
        var activeSubs = await db.Subscriptions
            .Where(s => s.CompanyId == companyId &&
                        (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .ToListAsync();
        foreach (var sub in activeSubs)
        {
            sub.Status = SubscriptionStatus.Canceled;
            sub.CanceledAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        // Cascade does NOT cover the whole company. Eight company-scoped
        // entities are mapped DeleteBehavior.Restrict — Contact, Deal, Lead,
        // HandoffEvent, CrmTask, Meeting, EmailLog and WhatsAppConversation —
        // so removing a company that has any CRM or WhatsApp history raises a
        // foreign key violation. They have to go first, deepest dependency
        // outwards, and the whole thing has to be one transaction: a failure
        // halfway through would otherwise leave a gutted but existing company.
        await using var tx = await db.Database.BeginTransactionAsync();

        await db.DealStageHistory.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
        await db.CrmTasks.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
        await db.Meetings.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
        await db.WhatsAppMessages.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
        await db.WhatsAppConversations.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
        await db.Deals.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
        await db.Leads.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
        await db.HandoffEvents.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
        // ContactTag cascades from Contact, so it goes with it.
        await db.Contacts.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();
        await db.EmailLogs.Where(x => x.CompanyId == companyId).ExecuteDeleteAsync();

        // The rest — agents (and their files), subscriptions, invitations,
        // memberships, pipelines and daily stats — are mapped Cascade.
        db.Companies.Remove(company);
        await db.SaveChangesAsync();

        await tx.CommitAsync();

        logger.LogInformation(
            "Company {CompanyId} deleted by user {UserId}", companyId, requestingUserId);

        // The Postgres rows are gone. Now erase the data that lives outside the
        // database — uploaded documents in S3 and conversation history in
        // DynamoDB — so "excluir" actually means deleted, per LGPD Art. 18.
        //
        // Best effort by design: the deletion is already committed and the
        // customer's request is honoured. A failure here must not resurrect the
        // company, so it is logged loudly for cleanup rather than rethrown. The
        // DynamoDB history also carries a 90-day TTL as a backstop.
        await PurgeExternalDataAsync(companyId, agentIds);
    }

    private async Task PurgeExternalDataAsync(int companyId, IReadOnlyCollection<int> agentIds)
    {
        // ── S3: agent files (agents/{id}/) and WhatsApp media (tenant/{companyId}/) ──
        try
        {
            var removed = 0;
            foreach (var agentId in agentIds)
                removed += await storage.DeleteByPrefixAsync($"agents/{agentId}/");
            removed += await storage.DeleteByPrefixAsync($"tenant/{companyId}/");
            logger.LogInformation(
                "Purged {Count} S3 objects for deleted company {CompanyId}", removed, companyId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Could not purge S3 objects for deleted company {CompanyId} — " +
                "manual cleanup needed for agents/{{id}}/ and tenant/{CompanyId}/", companyId, companyId);
        }

        // ── DynamoDB: chat history, owned by foji-ai-api ──
        var aiApiBase = configuration["AiApi:BaseUrl"]?.TrimEnd('/');
        var internalKey = configuration["InternalApiKey"];
        if (string.IsNullOrEmpty(aiApiBase) || string.IsNullOrEmpty(internalKey))
        {
            logger.LogError(
                "Cannot purge chat history for company {CompanyId}: AiApi:BaseUrl or InternalApiKey not configured",
                companyId);
            return;
        }

        try
        {
            var http = httpClientFactory.CreateClient();
            using var req = new HttpRequestMessage(
                HttpMethod.Post, $"{aiApiBase}/api/v1/internal/chat-history/purge")
            {
                Content = System.Net.Http.Json.JsonContent.Create(new { company_id = companyId }),
            };
            req.Headers.Add("X-Internal-Key", internalKey);
            var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                logger.LogError(
                    "Chat-history purge for company {CompanyId} returned {Status} — history may linger until its 90-day TTL",
                    companyId, resp.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Could not reach foji-ai-api to purge chat history for company {CompanyId} — " +
                "it will expire on its 90-day TTL", companyId);
        }
    }
}
