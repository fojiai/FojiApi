using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

public class Invitation : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int InviterUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public CompanyRole Role { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public User InviterUser { get; set; } = null!;
}
