namespace FojiApi.Core.Interfaces.Services;

public interface IAgentService
{
    Task<IEnumerable<AgentListItem>> GetAgentsAsync(int companyId);
    Task<AgentDetail> GetAgentAsync(int agentId);
    Task<AgentCreatedResult> CreateAgentAsync(int companyId, string name, string? description, string industryType, string? agentLanguage, string? userPrompt);
    Task<AgentUpdatedResult> UpdateAgentAsync(int agentId, string? name, string? description, string? userPrompt, bool? isActive, string? agentLanguage, bool? whatsAppEnabled, string? whatsAppPhoneNumberId, string? supportWhatsAppNumber, string? salesWhatsAppNumber, string? supportEmail, string? salesEmail);
    Task DeleteAgentAsync(int agentId);
    Task<string> RegenerateTokenAsync(int agentId);
    Task<EmbedCodeResult> GetEmbedCodeAsync(int agentId, string? widgetBaseUrl);
}

public record AgentListItem(int Id, string Name, string? Description, bool IsActive, string IndustryType, string AgentLanguage, string AgentToken, bool WhatsAppEnabled, int FileCount, DateTime CreatedAt);
public record AgentDetail(int Id, string Name, string? Description, bool IsActive, string IndustryType, string SystemPrompt, string? UserPrompt, string AgentLanguage, string AgentToken, bool WhatsAppEnabled, string? WhatsAppPhoneNumberId, int CompanyId, DateTime CreatedAt, DateTime UpdatedAt, IEnumerable<AgentFileItem> Files, string? SupportWhatsAppNumber, string? SalesWhatsAppNumber, string? SupportEmail, string? SalesEmail);
public record AgentFileItem(int Id, string FileName, long FileSizeBytes, string ProcessingStatus, DateTime CreatedAt);
public record AgentCreatedResult(int Id, string Name, string AgentToken);
public record AgentUpdatedResult(int Id, string Name, bool IsActive, DateTime UpdatedAt);
public record EmbedCodeResult(string EmbedCode, string AgentToken, string WidgetUrl);
