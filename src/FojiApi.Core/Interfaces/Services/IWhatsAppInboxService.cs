using FojiApi.Core.Enums;

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
    bool CanReplyFreeform,
    /// <summary>"Open" or "Resolved".</summary>
    string Status,
    DateTime? ResolvedAt,
    bool ResolvedAutomatically,
    /// <summary>The customer sent the last message — someone owes them a reply.</summary>
    bool AwaitingReply,
    /// <summary>The agent's WhatsApp mode: "Agent", "Inbox" or "Hybrid".</summary>
    string AgentMode,
    /// <summary>Hybrid: a person is handling it and the AI is quiet.</summary>
    bool HumanTakeover,
    /// <summary>"manual", "reply" or "ai_escalation".</summary>
    string? TakeoverReason,
    DateTime? TakeoverAt,
    /// <summary>Hybrid: the customer asked for a person and nobody has answered yet.</summary>
    DateTime? AwaitingHumanSince
);

/// <summary>What the worker needs to know after recording an inbound message.</summary>
/// <param name="Duplicate">Already recorded (a webhook retry) — don't answer it again.</param>
/// <param name="HumanTakeover">A person is handling this conversation — the AI must stay quiet.</param>
/// <param name="AwaitingHuman">The team was already called for this customer — the AI mustn't call them again.</param>
public record InboundRecordResult(int? ConversationId, bool Duplicate, bool HumanTakeover, bool AwaitingHuman = false);

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
    DateTime CreatedAt,
    bool IsAiGenerated = false
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
    Task<InboundRecordResult> RecordInboundAsync(
        int agentId, string phoneNumberId, string waId, string? profileName, string? wamId,
        string text, string messageType = "text",
        string? mediaS3Key = null, string? mediaContentType = null, string? mediaFileName = null);

    /// <summary>Hybrid: records a reply the AI sent, so the team sees the whole thread.</summary>
    Task RecordAiReplyAsync(int agentId, string waId, string body);

    /// <summary>
    /// Hybrid: the AI decided a person is needed. Hands the conversation to the
    /// team (the AI goes quiet in it) and notifies them.
    /// </summary>
    Task EscalateToHumanAsync(int agentId, string waId, string? customerMessage);

    /// <summary>Hybrid: a team member takes the conversation from the AI.</summary>
    Task<InboxConversationItem?> TakeoverAsync(int companyId, int conversationId, int userId);

    /// <summary>Hybrid: hands the conversation back to the AI.</summary>
    Task<InboxConversationItem?> ReleaseToAiAsync(int companyId, int conversationId);

    /// <summary>
    /// Hybrid safety net: conversations the AI escalated that no person picked up
    /// within <paramref name="timeout"/>. The customer gets one honest "the team
    /// will get back to you here" message and the AI resumes, while the inbox
    /// keeps flagging that they want a person. Returns how many were handled.
    /// </summary>
    Task<int> HandleUnansweredEscalationsAsync(TimeSpan timeout, CancellationToken ct = default);

    /// <param name="status">Filter to Open or Resolved; null returns both.</param>
    Task<IEnumerable<InboxConversationItem>> GetConversationsAsync(
        int companyId, int? agentId = null, InboxConversationStatus? status = null);

    Task<InboxThreadResult?> GetThreadAsync(int companyId, int conversationId);

    /// <summary>
    /// Sends a reply as the given team member, prefixed with their display name.
    /// Throws when the 24-hour window has closed.
    /// </summary>
    Task<InboxMessageItem> SendReplyAsync(int companyId, int conversationId, int userId, string text);

    Task MarkReadAsync(int companyId, int conversationId);

    /// <summary>Claims or releases a conversation. Pass null to unassign.</summary>
    Task<InboxConversationItem?> AssignAsync(int companyId, int conversationId, int? userId);

    /// <summary>Marks a conversation done. The customer writing again reopens it.</summary>
    Task<InboxConversationItem?> ResolveAsync(int companyId, int conversationId);

    Task<InboxConversationItem?> ReopenAsync(int companyId, int conversationId);

    /// <summary>
    /// Resolves open conversations idle for longer than <paramref name="idleFor"/>
    /// in which the team had the last word. A conversation where the customer sent
    /// the last message is never auto-resolved — that's someone waiting on a reply.
    /// Returns how many were resolved.
    /// </summary>
    Task<int> AutoResolveIdleAsync(TimeSpan idleFor, CancellationToken ct = default);
}
