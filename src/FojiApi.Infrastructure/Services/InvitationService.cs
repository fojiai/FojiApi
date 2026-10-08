using FojiApi.Core.Entities;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Core.Validation;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class InvitationService(FojiDbContext db, IJwtService jwtService) : IInvitationService
{
    public async Task<InvitationPreviewResult> GetInvitationAsync(string token)
    {
        var invitation = await LoadPendingAsync(token, includeInviter: true);

        var accountExists = await db.Users
            .AnyAsync(u => u.Email == invitation.Email && u.EmailVerifiedAt != null);

        return new InvitationPreviewResult(
            invitation.Email,
            invitation.CompanyId,
            invitation.Company.Name,
            $"{invitation.InviterUser.FirstName} {invitation.InviterUser.LastName}",
            invitation.Role.ToString().ToLower(),
            invitation.ExpiresAt,
            accountExists);
    }

    public async Task<AcceptInvitationResult> AcceptInvitationAsync(string token, int userId)
    {
        var invitation = await LoadPendingAsync(token);

        var user = await db.Users.FindAsync(userId)
            ?? throw new NotFoundException("User not found.");

        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("This invitation was sent to a different email address.");

        var existingMembership = await db.UserCompanies.FindAsync(userId, invitation.CompanyId);
        if (existingMembership != null)
            throw new DomainException("You are already a member of this company.");

        db.UserCompanies.Add(new UserCompany
        {
            UserId = userId,
            CompanyId = invitation.CompanyId,
            Role = invitation.Role,
            JoinedAt = DateTime.UtcNow,
            InvitedAt = invitation.CreatedAt
        });

        invitation.AcceptedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var userWithCompanies = await db.Users
            .Include(u => u.UserCompanies).ThenInclude(uc => uc.Company)
            .FirstAsync(u => u.Id == userId);

        var newToken = jwtService.GenerateToken(userWithCompanies, userWithCompanies.UserCompanies.Where(uc => uc.IsActive));

        return new AcceptInvitationResult(
            $"You have joined {invitation.Company.Name} as {invitation.Role.ToString().ToLower()}.",
            newToken,
            invitation.CompanyId);
    }

    public async Task<LoginResult> RegisterAndAcceptAsync(string token, string firstName, string lastName, string password)
    {
        PasswordValidator.Validate(password);

        var invitation = await LoadPendingAsync(token);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == invitation.Email);
        if (user is { EmailVerifiedAt: not null })
            throw new ConflictException("An account with this email already exists. Log in to accept the invitation.");

        if (user == null)
        {
            user = new User { Email = invitation.Email };
            db.Users.Add(user);
        }

        // A signup that never verified its email proved nothing about who owns
        // the address; this link does, so the invited person takes it over.
        user.FirstName = firstName.Trim();
        user.LastName = lastName.Trim();
        user.HashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        user.EmailVerifiedAt = DateTime.UtcNow;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiresAt = null;

        db.UserCompanies.Add(new UserCompany
        {
            User = user,
            CompanyId = invitation.CompanyId,
            Role = invitation.Role,
            JoinedAt = DateTime.UtcNow,
            InvitedAt = invitation.CreatedAt
        });

        invitation.AcceptedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var saved = await db.Users
            .Include(u => u.UserCompanies).ThenInclude(uc => uc.Company)
            .FirstAsync(u => u.Id == user.Id);
        var activeCompanies = saved.UserCompanies.Where(uc => uc.IsActive).ToList();

        return new LoginResult(
            Token: jwtService.GenerateToken(saved, activeCompanies),
            UserId: saved.Id,
            Email: saved.Email,
            FirstName: saved.FirstName,
            LastName: saved.LastName,
            IsSuperAdmin: saved.IsSuperAdmin,
            Companies: activeCompanies.Select(uc => new UserCompanyResult(
                uc.CompanyId, uc.Company.Name, uc.Company.Slug, uc.Role.ToString().ToLower())));
    }

    private async Task<Invitation> LoadPendingAsync(string token, bool includeInviter = false)
    {
        var query = db.Invitations.Include(i => i.Company).AsQueryable();
        if (includeInviter) query = query.Include(i => i.InviterUser);

        var invitation = await query.FirstOrDefaultAsync(i => i.Token == token)
            ?? throw new NotFoundException("Invitation not found.");

        if (invitation.AcceptedAt != null)
            throw new DomainException("This invitation has already been accepted.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
            throw new DomainException("This invitation has expired.");

        return invitation;
    }
}
