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

    /// <summary>The deduped CRM contact this raw capture event rolls up to (null for anonymous captures).</summary>
    public int? ContactId { get; set; }

    // Navigation
    public Agent Agent { get; set; } = null!;
    public Company Company { get; set; } = null!;
    public Contact? Contact { get; set; }
}
