using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

/// <summary>A single message in a WhatsApp conversation, inbound or outbound.</summary>
public class WhatsAppMessage : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int ConversationId { get; set; }

    public MessageDirection Direction { get; set; }

    /// <summary>The message text as it was sent or received, prefix included.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Meta's message id (wamid). Used to dedupe webhook retries.</summary>
    public string? WamId { get; set; }

    /// <summary>Team member who sent an outbound message; null for inbound and AI replies.</summary>
    public int? SentByUserId { get; set; }

    /// <summary>
    /// The display name stamped on the message at send time. Snapshotted rather
    /// than joined, so renaming a member never rewrites history.
    /// </summary>
    public string? SenderDisplayName { get; set; }

    // Navigation
    public WhatsAppConversation Conversation { get; set; } = null!;
    public User? SentByUser { get; set; }
}
