using FojiApi.Core.Entities;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Services;

public class HandoffService(
    FojiDbContext db,
    IEmailService emailService,
    ILogger<HandoffService> logger) : IHandoffService
{
    public async Task<HandoffCreatedResult> CreateHandoffAsync(
        int agentId, string sessionId, string? userMessage, string source)
    {
        var agent = await db.Agents.Include(a => a.Company).FirstOrDefaultAsync(a => a.Id == agentId)
            ?? throw new NotFoundException("Agent not found.");

        var handoff = new HandoffEvent
        {
            AgentId = agentId,
            CompanyId = agent.CompanyId,
            SessionId = sessionId,
            UserMessage = userMessage?.Trim().Length > 0 ? userMessage.Trim() : null,
            Source = source,
        };

        db.HandoffEvents.Add(handoff);

        // Send email notification if configured
        if (!string.IsNullOrWhiteSpace(agent.HandoffNotifyEmail))
        {
            try
            {
                await emailService.SendHandoffNotificationAsync(
                    agent.HandoffNotifyEmail,
                    agent.Name,
                    agent.Company.Name,
                    sessionId,
                    handoff.UserMessage);

                handoff.NotificationSent = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send handoff email notification for agent {AgentId}", agentId);
            }
        }

        await db.SaveChangesAsync();

        return new HandoffCreatedResult(handoff.Id, handoff.AgentId, handoff.CompanyId, handoff.SessionId);
    }

    public async Task<IEnumerable<HandoffListItem>> GetHandoffsAsync(int companyId, int? agentId = null)
    {
        var query = db.HandoffEvents
            .Include(h => h.Agent)
            .Where(h => h.CompanyId == companyId);

        if (agentId.HasValue)
            query = query.Where(h => h.AgentId == agentId.Value);

        return await query
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new HandoffListItem(
                h.Id, h.AgentId, h.Agent.Name,
                h.SessionId, h.UserMessage,
                h.Source, h.NotificationSent, h.CreatedAt))
            .ToListAsync();
    }
}
