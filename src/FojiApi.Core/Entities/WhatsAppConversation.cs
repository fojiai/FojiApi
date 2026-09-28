using FojiApi.Core.Enums;

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

    /// <summary>
    /// Open until the team is done with it. Resolved conversations drop out of the
    /// default inbox view; the customer writing again reopens them.
    /// </summary>
    public InboxConversationStatus Status { get; set; } = InboxConversationStatus.Open;

    public DateTime? ResolvedAt { get; set; }

    /// <summary>True when the idle sweep resolved it rather than a person.</summary>
    public bool ResolvedAutomatically { get; set; }

    /// <summary>
    /// Hybrid mode: a person is handling this conversation, so the AI stays
    /// quiet in it. Cleared when it's handed back or resolved.
    /// </summary>
    public bool HumanTakeover { get; set; }

    public DateTime? TakeoverAt { get; set; }

    /// <summary>"manual" (took it over), "reply" (answered from the inbox) or
    /// "ai_escalation" (the AI called the team).</summary>
    public string? TakeoverReason { get; set; }

    public int? TakeoverByUserId { get; set; }

    /// <summary>
    /// Hybrid: when the customer's request for a person started waiting. Set when
    /// the AI escalates; cleared only once a person replies, takes the
    /// conversation, or it's resolved. Survives the escalation timeout — the AI
    /// may be answering again, but the customer still wants a person.
    /// </summary>
    public DateTime? AwaitingHumanSince { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public Agent Agent { get; set; } = null!;
    public Contact? Contact { get; set; }
    public User? AssignedUser { get; set; }
    public ICollection<WhatsAppMessage> Messages { get; set; } = new List<WhatsAppMessage>();
}
