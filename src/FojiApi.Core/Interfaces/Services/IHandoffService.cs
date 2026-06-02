namespace FojiApi.Core.Interfaces.Services;

public interface IHandoffService
{
    /// <summary>
    /// Creates a HandoffEvent record and sends notifications to configured contacts.
    /// Called from the internal endpoint (invoked by foji-ai-api).
    /// </summary>
    Task<HandoffCreatedResult> CreateHandoffAsync(int agentId, string sessionId, string? userMessage, string source);

    Task<IEnumerable<HandoffListItem>> GetHandoffsAsync(int companyId, int? agentId = null);
}

public record HandoffCreatedResult(int Id, int AgentId, int CompanyId, string SessionId);
public record HandoffListItem(
    int Id,
    int AgentId,
    string AgentName,
    string SessionId,
    string? UserMessage,
    string Source,
    bool NotificationSent,
    DateTime CreatedAt
);
