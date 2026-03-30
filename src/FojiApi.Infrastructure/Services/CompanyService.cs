using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class CompanyService(FojiDbContext db, IJwtService jwtService, IEmailService emailService) : ICompanyService
{
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
        var user = await db.Users.Include(u => u.UserCompanies).FirstAsync(u => u.Id == userId);

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

        // Remove related entities — EF cascade handles the rest via FK constraints,
        // but we need to explicitly cancel Stripe subscriptions first if active.
        // (Stripe webhook will re-sync, but we also cancel in DB immediately.)
        var activeSubs = await db.Subscriptions
            .Where(s => s.CompanyId == companyId &&
                        (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing))
            .ToListAsync();
        foreach (var sub in activeSubs)
        {
            sub.Status = SubscriptionStatus.Canceled;
            sub.CanceledAt = DateTime.UtcNow;
        }

        db.Companies.Remove(company);
        await db.SaveChangesAsync();
    }
}
