namespace FojiApi.Core.Interfaces.Services;

public record InboxConversationItem(
    int Id,
    int AgentId,
    string AgentName,
    string ContactWaId,
    string? ContactName,
    int? ContactId,
    int? AssignedUserId,
    string? AssignedUserName,
    string? LastMessagePreview,
    DateTime LastMessageAt,
    DateTime? LastInboundAt,
    int UnreadCount,
    /// <summary>False once Meta's 24-hour customer service window has closed.</summary>
    bool CanReplyFreeform
);

public record InboxMessageItem(
    int Id,
    string Direction,
    string Body,
    string MessageType,
    /// <summary>Short-lived presigned URL for media, null for text messages.</summary>
    string? MediaUrl,
    string? MediaContentType,
    string? MediaFileName,
    int? SentByUserId,
    string? SenderDisplayName,
    DateTime CreatedAt
);

public record InboxThreadResult(
    InboxConversationItem Conversation,
    IEnumerable<InboxMessageItem> Messages
);

public interface IWhatsAppInboxService
{
    /// <summary>
    /// Records an inbound message, creating the conversation on first contact.
    /// Idempotent on the wamid — Meta retries and batches webhook deliveries.
    /// </summary>
    Task RecordInboundAsync(
        int agentId, string phoneNumberId, string waId, string? profileName, string? wamId,
        string text, string messageType = "text",
        string? mediaS3Key = null, string? mediaContentType = null, string? mediaFileName = null);

    Task<IEnumerable<InboxConversationItem>> GetConversationsAsync(int companyId, int? agentId = null);

    Task<InboxThreadResult?> GetThreadAsync(int companyId, int conversationId);

    /// <summary>
    /// Sends a reply as the given team member, prefixed with their display name.
    /// Throws when the 24-hour window has closed.
    /// </summary>
    Task<InboxMessageItem> SendReplyAsync(int companyId, int conversationId, int userId, string text);

    Task MarkReadAsync(int companyId, int conversationId);

    /// <summary>Claims or releases a conversation. Pass null to unassign.</summary>
    Task<InboxConversationItem?> AssignAsync(int companyId, int conversationId, int? userId);
}
