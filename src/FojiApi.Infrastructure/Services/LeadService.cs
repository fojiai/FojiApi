using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class LeadService(FojiDbContext db) : ILeadService
{
    public async Task<IEnumerable<LeadListItem>> GetLeadsAsync(int companyId, int? agentId = null)
    {
        var query = db.Leads
            .Include(l => l.Agent)
            .Where(l => l.CompanyId == companyId);

        if (agentId.HasValue)
            query = query.Where(l => l.AgentId == agentId.Value);

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LeadListItem(
                l.Id, l.AgentId, l.Agent.Name,
                l.Name, l.Email, l.Phone,
                l.SessionId, l.Source, l.CreatedAt))
            .ToListAsync();
    }

    public async Task<int> GetLeadCountAsync(int companyId)
    {
        return await db.Leads.CountAsync(l => l.CompanyId == companyId);
    }
}
