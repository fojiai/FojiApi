namespace FojiApi.Core.Entities;

public class User : BaseEntity
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string HashedPassword { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsSuperAdmin { get; set; } = false;
    public DateTime? EmailVerifiedAt { get; set; }
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiresAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    // Navigation
    public ICollection<UserCompany> UserCompanies { get; set; } = [];
    public ICollection<Invitation> SentInvitations { get; set; } = [];
    public ICollection<SystemAdminInvitation> SentSystemAdminInvitations { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
