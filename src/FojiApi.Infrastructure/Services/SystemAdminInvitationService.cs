using FojiApi.Core.Entities;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Core.Validation;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class SystemAdminInvitationService(
    FojiDbContext db,
    IEmailService emailService) : ISystemAdminInvitationService
{
    public async Task InviteAsync(int invitedByUserId, string email)
    {
        email = email.ToLower().Trim();

        if (await db.Users.AnyAsync(u => u.Email == email && u.IsSuperAdmin))
            throw new InvalidOperationException("A system admin with this email already exists.");

        if (await db.Users.AnyAsync(u => u.Email == email))
            throw new InvalidOperationException("A user with this email already exists. Promote them to system admin instead.");

        // Cancel any existing pending invite for this email
        var existing = await db.SystemAdminInvitations
            .Where(i => i.Email == email && i.AcceptedAt == null)
            .ToListAsync();
        db.SystemAdminInvitations.RemoveRange(existing);

        var inviter = await db.Users.FindAsync(invitedByUserId)
            ?? throw new InvalidOperationException("Inviting user not found.");

        var invitation = new SystemAdminInvitation
        {
            InvitedByUserId = invitedByUserId,
            Email = email,
            Token = Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        db.SystemAdminInvitations.Add(invitation);
        await db.SaveChangesAsync();

        await emailService.SendSystemAdminInvitationAsync(
            email, $"{inviter.FirstName} {inviter.LastName}", invitation.Token);
    }

    public async Task<object> GetInvitationPreviewAsync(string token)
    {
        var invitation = await db.SystemAdminInvitations
            .Include(i => i.InvitedByUser)
            .FirstOrDefaultAsync(i => i.Token == token)
            ?? throw new KeyNotFoundException("Invitation not found.");

        if (invitation.AcceptedAt != null)
            throw new InvalidOperationException("This invitation has already been accepted.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("This invitation has expired.");

        return new
        {
            invitation.Email,
            invitedBy = $"{invitation.InvitedByUser.FirstName} {invitation.InvitedByUser.LastName}",
            invitation.ExpiresAt
        };
    }

    public async Task AcceptAsync(string token, string firstName, string lastName, string password)
    {
        PasswordValidator.Validate(password);

        var invitation = await db.SystemAdminInvitations
            .FirstOrDefaultAsync(i => i.Token == token)
            ?? throw new KeyNotFoundException("Invitation not found.");

        if (invitation.AcceptedAt != null)
            throw new InvalidOperationException("This invitation has already been accepted.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("This invitation has expired.");

        if (await db.Users.AnyAsync(u => u.Email == invitation.Email))
            throw new InvalidOperationException("An account with this email already exists.");

        var user = new User
        {
            Email = invitation.Email,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(password),
            IsSuperAdmin = true,
            IsActive = true,
            EmailVerifiedAt = DateTime.UtcNow // Admin invites are pre-verified
        };

        db.Users.Add(user);
        invitation.AcceptedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<object> ListPendingAsync()
    {
        var invitations = await db.SystemAdminInvitations
            .Include(i => i.InvitedByUser)
            .Where(i => i.AcceptedAt == null && i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id, i.Email,
                invitedBy = $"{i.InvitedByUser.FirstName} {i.InvitedByUser.LastName}",
                i.ExpiresAt, i.CreatedAt
            })
            .ToListAsync();

        return invitations;
    }

    public async Task RevokeAsync(int invitationId)
    {
        var invitation = await db.SystemAdminInvitations.FindAsync(invitationId)
            ?? throw new KeyNotFoundException("Invitation not found.");

        db.SystemAdminInvitations.Remove(invitation);
        await db.SaveChangesAsync();
    }
}
