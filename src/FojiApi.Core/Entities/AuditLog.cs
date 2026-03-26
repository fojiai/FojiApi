namespace FojiApi.Core.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Company? Company { get; set; }
    public User? User { get; set; }
}
