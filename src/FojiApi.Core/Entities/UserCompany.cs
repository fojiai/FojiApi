using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

public class UserCompany
{
    public int UserId { get; set; }
    public int CompanyId { get; set; }
    public CompanyRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; }
    public DateTime? InvitedAt { get; set; }

    /// <summary>
    /// What customers see when this member replies from the WhatsApp inbox
    /// (e.g. "Ana" or "Ana | Suporte"). Falls back to the user's first name.
    /// </summary>
    public string? WhatsAppDisplayName { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
