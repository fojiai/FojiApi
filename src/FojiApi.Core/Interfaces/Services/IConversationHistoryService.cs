namespace FojiApi.Core.Interfaces.Services;

/// <summary>One chat in the history list.</summary>
/// <param name="Kind">"inbox" (a WhatsApp thread from the shared inbox) or "session" (an indexed AI chat).</param>
/// <param name="Channel">"site" or "whatsapp".</param>
public record HistoryItem(
    string Kind,
    int Id,
    int AgentId,
    string AgentName,
    string Channel,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    DateTime StartedAt,
    DateTime LastMessageAt,
    string? Preview,
    /// <summary>A person from the team wrote in this chat.</summary>
    bool TeamReplied
);

/// <param name="From">"customer", "ai" or "team".</param>
public record HistoryMessage(
    string From,
    string Text,
    DateTime At,
    string? SenderName = null,
    string? MediaUrl = null,
    string? MediaContentType = null,
    string? MediaFileName = null
);

/// <param name="MessagesUnavailable">The messages exist but couldn't be loaded right now (not the same as an empty or expired chat).</param>
public record HistoryThread(HistoryItem Conversation, IEnumerable<HistoryMessage> Messages, bool MessagesUnavailable = false);

public record HistoryPage(IEnumerable<HistoryItem> Items, int Total);

public interface IConversationHistoryService
{
    Task<HistoryPage> ListAsync(int companyId, int? agentId, string? channel, string? search, int page, int pageSize);
    Task<HistoryThread?> GetAsync(int companyId, string kind, int id);
}
