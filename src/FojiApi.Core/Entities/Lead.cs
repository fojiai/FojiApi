namespace FojiApi.Core.Entities;

public class Lead : BaseEntity
{
    public int Id { get; set; }
    public int AgentId { get; set; }
    public int CompanyId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Source { get; set; } = "widget"; // "widget" | "whatsapp"

    // Navigation
    public Agent Agent { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
