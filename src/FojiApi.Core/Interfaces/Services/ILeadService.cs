namespace FojiApi.Core.Interfaces.Services;

public interface ILeadService
{
    Task<IEnumerable<LeadListItem>> GetLeadsAsync(int companyId, int? agentId = null);
    Task<int> GetLeadCountAsync(int companyId);
}

public record LeadListItem(
    int Id,
    int AgentId,
    string AgentName,
    string? Name,
    string? Email,
    string? Phone,
    string SessionId,
    string Source,
    DateTime CreatedAt
);
