using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

public class Agent : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public IndustryType IndustryType { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public string? UserPrompt { get; set; }
    public AgentLanguage AgentLanguage { get; set; } = AgentLanguage.PtBr;
    public string AgentToken { get; set; } = string.Empty;
    public bool WhatsAppEnabled { get; set; } = false;
    public string? WhatsAppPhoneNumberId { get; set; }

    // Escalation contacts (shown in system prompt when set; plan-gated)
    public string? SupportWhatsAppNumber { get; set; }
    public string? SalesWhatsAppNumber { get; set; }
    public string? SupportEmail { get; set; }
    public string? SalesEmail { get; set; }

    // Widget customization
    public string? WelcomeMessage { get; set; }
    public string? ConversationStarters { get; set; } // JSON array: ["q1","q2","q3","q4"]
    public string? WidgetPrimaryColor { get; set; }
    public string? WidgetTitle { get; set; }
    public string? WidgetPlaceholder { get; set; }
    public string? WidgetPosition { get; set; } // "left" or "right"

    // Navigation
    public Company Company { get; set; } = null!;
    public ICollection<AgentFile> Files { get; set; } = [];
}
