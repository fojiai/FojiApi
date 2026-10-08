using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FojiApi.Core.Enums;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Services;

/// <summary>
/// "Histórico de conversas": every chat an owner's agents had, website and
/// WhatsApp, in one list.
///
/// Two sources, merged:
///  - WhatsApp threads from the shared inbox (Inbox/Hybrid mode). These carry
///    the team's replies with names, so they win for that customer.
///  - ChatSessions, the index foji-ai-api writes for every AI exchange. The
///    messages are fetched from foji-ai-api's DynamoDB log on open.
/// Both keep 90 days, matching the log's TTL and the privacy policy.
/// </summary>
public class ConversationHistoryService(
    FojiDbContext db,
    IWhatsAppInboxService inbox,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<ConversationHistoryService> logger) : IConversationHistoryService
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(90);
    /// <summary>Per source. The list is merged in memory; this keeps it bounded.</summary>
    private const int MaxPerSource = 1000;

    public async Task<HistoryPage> ListAsync(int companyId, int? agentId, string? channel, string? search, int page, int pageSize)
    {
        var since = DateTime.UtcNow - Retention;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var wantSite = channel is null or "" or "site";
        var wantWhatsApp = channel is null or "" or "whatsapp";

        var items = new List<HistoryItem>();
        var inboxKeys = new HashSet<(int, string)>();

        if (wantWhatsApp)
        {
            var threads = await db.WhatsAppConversations
                .Where(c => c.CompanyId == companyId && c.LastMessageAt >= since)
                .Where(c => agentId == null || c.AgentId == agentId)
                .OrderByDescending(c => c.LastMessageAt)
                .Take(MaxPerSource)
                .Select(c => new
                {
                    c.Id, c.AgentId, AgentName = c.Agent.Name, c.ContactWaId, c.ContactName, c.CreatedAt, c.LastMessageAt,
                    ContactEmail = c.Contact != null ? c.Contact.Email : null,
                    Preview = db.WhatsAppMessages.Where(m => m.ConversationId == c.Id)
                        .OrderByDescending(m => m.CreatedAt).Select(m => m.Body).FirstOrDefault(),
                    TeamReplied = db.WhatsAppMessages.Any(m => m.ConversationId == c.Id && m.SentByUserId != null),
                })
                .ToListAsync();

            foreach (var t in threads)
            {
                inboxKeys.Add((t.AgentId, t.ContactWaId));
                items.Add(new HistoryItem("inbox", t.Id, t.AgentId, t.AgentName, "whatsapp",
                    t.ContactName, t.ContactWaId, t.ContactEmail, t.CreatedAt, t.LastMessageAt,
                    Trim(t.Preview), t.TeamReplied));
            }
        }

        var sessions = await db.ChatSessions
            .Where(s => s.CompanyId == companyId && s.LastMessageAt >= since)
            .Where(s => agentId == null || s.AgentId == agentId)
            .Where(s => (wantSite && s.Channel == "site") || (wantWhatsApp && s.Channel == "whatsapp"))
            .OrderByDescending(s => s.LastMessageAt)
            .Take(MaxPerSource)
            .Select(s => new
            {
                s.Id, s.AgentId, AgentName = s.Agent.Name, s.Channel, s.SessionId, s.ContactWaId, s.ContactName,
                s.StartedAt, s.LastMessageAt, s.LastPreview,
                // A website visitor is anonymous unless they left their details.
                Lead = db.Leads.Where(l => l.AgentId == s.AgentId && l.SessionId == s.SessionId)
                    .OrderByDescending(l => l.CreatedAt)
                    .Select(l => new { l.Name, l.Email, l.Phone }).FirstOrDefault(),
            })
            .ToListAsync();

        foreach (var s in sessions)
        {
            if (s.Channel == "whatsapp" && s.ContactWaId != null && inboxKeys.Contains((s.AgentId, s.ContactWaId)))
                continue;
            items.Add(new HistoryItem("session", s.Id, s.AgentId, s.AgentName, s.Channel,
                s.Lead?.Name ?? s.ContactName,
                s.Lead?.Phone ?? s.ContactWaId,
                s.Lead?.Email,
                s.StartedAt, s.LastMessageAt, s.LastPreview, TeamReplied: false));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            items = items.Where(i =>
                Contains(i.ContactName, q) || Contains(i.ContactPhone, q) || Contains(i.ContactEmail, q)
                || Contains(i.Preview, q) || Contains(i.AgentName, q)).ToList();
        }

        var ordered = items.OrderByDescending(i => i.LastMessageAt).ToList();
        return new HistoryPage(ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList(), ordered.Count);
    }

    public async Task<HistoryThread?> GetAsync(int companyId, string kind, int id)
    {
        if (kind == "inbox")
        {
            // Opening history is reading, not answering: don't touch unread counts.
            var thread = await inbox.GetThreadAsync(companyId, id);
            if (thread == null) return null;
            var c = thread.Conversation;
            var started = await db.WhatsAppConversations.Where(x => x.Id == id).Select(x => x.CreatedAt).FirstAsync();
            var messages = thread.Messages.OrderBy(m => m.CreatedAt).Select(m => new HistoryMessage(
                m.Direction == "Inbound" ? "customer" : (m.SentByUserId != null && !m.IsAiGenerated ? "team" : "ai"),
                m.Body, m.CreatedAt, m.SentByUserId != null && !m.IsAiGenerated ? m.SenderDisplayName : null,
                m.MediaUrl, m.MediaContentType, m.MediaFileName)).ToList();
            var item = new HistoryItem("inbox", c.Id, c.AgentId, c.AgentName, "whatsapp", c.ContactName, c.ContactWaId,
                null, started, c.LastMessageAt, Trim(c.LastMessagePreview), messages.Any(m => m.From == "team"));
            return new HistoryThread(item, messages);
        }

        if (kind != "session") return null;

        var s = await db.ChatSessions
            .Where(x => x.Id == id && x.CompanyId == companyId)
            .Select(x => new
            {
                x.Id, x.AgentId, AgentName = x.Agent.Name, x.Channel, x.SessionId, x.ContactWaId, x.ContactName,
                x.StartedAt, x.LastMessageAt, x.LastPreview,
                Lead = db.Leads.Where(l => l.AgentId == x.AgentId && l.SessionId == x.SessionId)
                    .OrderByDescending(l => l.CreatedAt).Select(l => new { l.Name, l.Email, l.Phone }).FirstOrDefault(),
            })
            .FirstOrDefaultAsync();
        if (s == null) return null;

        var header = new HistoryItem("session", s.Id, s.AgentId, s.AgentName, s.Channel,
            s.Lead?.Name ?? s.ContactName, s.Lead?.Phone ?? s.ContactWaId, s.Lead?.Email,
            s.StartedAt, s.LastMessageAt, s.LastPreview, false);
        var log = await FetchLogAsync(s.SessionId, s.AgentId);
        return new HistoryThread(header, log ?? [], MessagesUnavailable: log == null);
    }

    /// <summary>The AI's log for this chat, from foji-ai-api. Null if it can't be reached.</summary>
    private async Task<List<HistoryMessage>?> FetchLogAsync(string sessionId, int agentId)
    {
        var baseUrl = configuration["AiApi:BaseUrl"]?.TrimEnd('/');
        var key = configuration["InternalApiKey"];
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(key))
        {
            logger.LogError("Conversation history: AiApi:BaseUrl or InternalApiKey not configured");
            return null;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v1/internal/chat-history/messages")
            {
                Content = JsonContent.Create(new { session_id = sessionId, agent_id = agentId }),
            };
            req.Headers.Add("X-Internal-Key", key);
            var resp = await httpClientFactory.CreateClient().SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogError("Chat log fetch for {SessionId} from {BaseUrl} returned {Status}", sessionId, baseUrl, resp.StatusCode);
                return null;
            }
            var body = await resp.Content.ReadFromJsonAsync<LogResponse>();
            return (body?.Messages ?? [])
                .Select(m => new HistoryMessage(
                    m.Role == "user" ? "customer" : "ai",
                    m.Content,
                    DateTimeOffset.FromUnixTimeMilliseconds(m.Timestamp).UtcDateTime))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not fetch the chat log for {SessionId} from {BaseUrl}", sessionId, baseUrl);
            return null;
        }
    }

    private static string? Trim(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Length <= 140 ? text : text[..140] + "…";

    private static bool Contains(string? value, string q) =>
        value != null && value.Contains(q, StringComparison.OrdinalIgnoreCase);

    private sealed record LogResponse([property: JsonPropertyName("messages")] List<LogMessage> Messages);
    private sealed record LogMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("timestamp")] long Timestamp);
}
