namespace FojiApi.Core.Entities;

/// <summary>
/// Index of one chat between an agent and a customer, so owners can browse
/// their conversation history. The messages themselves live in foji-ai-api's
/// DynamoDB log (90-day TTL) — this row only says the chat exists, who it was
/// with and when it last moved. foji-ai-api writes it on every exchange.
///
/// WhatsApp threads that went through the shared inbox are listed from
/// WhatsAppConversations instead (that thread also has the team's replies), so
/// a row here whose (AgentId, ContactWaId) has an inbox thread is skipped.
/// </summary>
public class ChatSession : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int AgentId { get; set; }

    /// <summary>"site" or "whatsapp".</summary>
    public string Channel { get; set; } = "site";

    /// <summary>The DynamoDB partition key the messages are stored under.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>WhatsApp only: the customer's number.</summary>
    public string? ContactWaId { get; set; }

    /// <summary>WhatsApp profile name, when Meta sent one.</summary>
    public string? ContactName { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime LastMessageAt { get; set; }

    /// <summary>The latest message, trimmed, for the list.</summary>
    public string? LastPreview { get; set; }

    public Company Company { get; set; } = null!;
    public Agent Agent { get; set; } = null!;
}
