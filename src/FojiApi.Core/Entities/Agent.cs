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
    // Per-tenant Meta Cloud API access token (AES-256-GCM, same scheme as calendar tokens).
    // Phase 0: pasted by the owner; Phase 1: populated by Embedded Signup. foji-worker decrypts to send.
    public string? WhatsAppAccessTokenEncrypted { get; set; }
    /// <summary>The customer's WhatsApp Business Account, captured by Embedded Signup.
    /// Needed to subscribe our app to their webhooks and to manage templates later.</summary>
    public string? WhatsAppBusinessAccountId { get; set; }
    /// <summary>Two-step PIN we set when registering their number on Cloud API.
    /// Stored encrypted because re-registering the number requires it.</summary>
    public string? WhatsAppPinEncrypted { get; set; }
    /// <summary>When the Meta token dies. Null for manually-pasted tokens, which
    /// are permanent System User tokens and need no refreshing.</summary>
    public DateTime? WhatsAppTokenExpiresAt { get; set; }
    /// <summary>Set when we can no longer talk to Meta for this agent — a refresh
    /// failed, or a send came back unauthorized. The UI asks the owner to
    /// reconnect, so a dead channel announces itself instead of going quiet.</summary>
    public bool WhatsAppNeedsReconnect { get; set; }
    /// <summary>
    /// Meta is refusing to deliver because the customer's WhatsApp Business
    /// Account has no usable payment method (error 131042). Deliberately
    /// separate from NeedsReconnect: reconnecting does nothing here, and telling
    /// someone to reconnect when they need to add a card is worse than silence.
    /// </summary>
    public bool WhatsAppBillingIssue { get; set; }
    /// <summary>Agent = the AI replies automatically; Inbox = humans reply from the shared inbox.</summary>
    public WhatsAppMode WhatsAppMode { get; set; } = WhatsAppMode.Agent;

    // Escalation contacts (shown in system prompt when set; plan-gated)
    public string? SupportWhatsAppNumber { get; set; }
    public string? SalesWhatsAppNumber { get; set; }
    public string? SupportEmail { get; set; }
    public string? SalesEmail { get; set; }

    // Response style — controls tone injected into system prompt
    public string? ResponseStyle { get; set; } // "Professional" | "Friendly" | "Concise"

    // Widget customization
    public string? WelcomeMessage { get; set; }
    public string? ConversationStarters { get; set; } // JSON array: ["q1","q2","q3","q4"]
    public string? WidgetPrimaryColor { get; set; }
    public string? WidgetTitle { get; set; }
    public string? WidgetPlaceholder { get; set; }
    public string? WidgetPosition { get; set; } // "left" or "right"

    // Lead capture
    public bool LeadCaptureEnabled { get; set; } = false;
    public string? LeadCapturePrompt { get; set; } // Custom message shown above the lead form

    // Human handoff
    public bool HandoffEnabled { get; set; } = false;
    public string? HandoffNotifyEmail { get; set; } // Email to notify when handoff is requested
    public string? HandoffNotifyWhatsApp { get; set; } // WhatsApp number to notify when handoff is requested
    public string? HandoffMessage { get; set; } // Custom message shown to user after requesting handoff

    // Navigation
    public Company Company { get; set; } = null!;
    public ICollection<AgentFile> Files { get; set; } = [];
    public ICollection<Lead> Leads { get; set; } = [];
    public AgentCalendarConnection? CalendarConnection { get; set; }
}
