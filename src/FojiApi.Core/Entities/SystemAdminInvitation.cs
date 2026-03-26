namespace FojiApi.Core.Entities;

public class SystemAdminInvitation : BaseEntity
{
    public int Id { get; set; }
    public int InvitedByUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    // Navigation
    public User InvitedByUser { get; set; } = null!;
}
