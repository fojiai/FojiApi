namespace FojiApi.Core.Entities;

/// <summary>
/// One WhatsApp thread between a business number and a customer, backing the
/// shared team inbox. Identified by (AgentId, ContactWaId) — the wa_id Meta
/// returns, which is the only stable identity for the customer.
/// </summary>
public class WhatsAppConversation : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int AgentId { get; set; }

    /// <summary>Our Meta phone number that received the message.</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>The customer's wa_id. Never derive this from what we dialled — BR
    /// numbers come back with or without the ninth digit.</summary>
    public string ContactWaId { get; set; } = string.Empty;

    /// <summary>The customer's WhatsApp profile name, when Meta sends one.</summary>
    public string? ContactName { get; set; }

    /// <summary>Linked CRM contact, when one was matched or created.</summary>
    public int? ContactId { get; set; }

    public DateTime LastMessageAt { get; set; }

    /// <summary>
    /// When the customer last messaged us. Meta's 24-hour customer service window
    /// is measured from this; outside it, free-form replies are rejected.
    /// </summary>
    public DateTime? LastInboundAt { get; set; }

    public int UnreadCount { get; set; }

    /// <summary>Team member who claimed this conversation, so two people don't answer at once.</summary>
    public int? AssignedUserId { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public Agent Agent { get; set; } = null!;
    public Contact? Contact { get; set; }
    public User? AssignedUser { get; set; }
    public ICollection<WhatsAppMessage> Messages { get; set; } = new List<WhatsAppMessage>();
}
