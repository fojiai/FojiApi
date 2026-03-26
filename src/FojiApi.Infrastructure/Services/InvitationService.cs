using FojiApi.Core.Entities;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class InvitationService(FojiDbContext db, IJwtService jwtService) : IInvitationService
{
    public async Task<InvitationPreviewResult> GetInvitationAsync(string token)
    {
        var invitation = await db.Invitations
            .Include(i => i.Company)
            .Include(i => i.InviterUser)
            .FirstOrDefaultAsync(i => i.Token == token)
            ?? throw new NotFoundException("Invitation not found.");

        if (invitation.AcceptedAt != null)
            throw new DomainException("This invitation has already been accepted.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
            throw new DomainException("This invitation has expired.");

        return new InvitationPreviewResult(
            invitation.Email,
            invitation.Company.Name,
            $"{invitation.InviterUser.FirstName} {invitation.InviterUser.LastName}",
            invitation.Role.ToString().ToLower(),
            invitation.ExpiresAt);
    }

    public async Task<AcceptInvitationResult> AcceptInvitationAsync(string token, int userId)
    {
        var invitation = await db.Invitations
            .Include(i => i.Company)
            .FirstOrDefaultAsync(i => i.Token == token)
            ?? throw new NotFoundException("Invitation not found.");

        if (invitation.AcceptedAt != null)
            throw new DomainException("This invitation has already been accepted.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
            throw new DomainException("This invitation has expired.");

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
            .Include(u => u.UserCompanies)
            .FirstAsync(u => u.Id == userId);

        var newToken = jwtService.GenerateToken(userWithCompanies, userWithCompanies.UserCompanies.Where(uc => uc.IsActive));

        return new AcceptInvitationResult(
            $"You have joined {invitation.Company.Name} as {invitation.Role.ToString().ToLower()}.",
            newToken);
    }
}
