namespace FojiApi.Core.Entities;

public class HandoffEvent : BaseEntity
{
    public int Id { get; set; }
    public int AgentId { get; set; }
    public int CompanyId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string? UserMessage { get; set; } // Last user message before handoff
    public string Source { get; set; } = "widget"; // "widget" | "whatsapp"
    public bool NotificationSent { get; set; } = false;

    // Navigation
    public Agent Agent { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
