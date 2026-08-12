using System.Net.Http.Json;
using System.Text.Json;
using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Services;

/// <summary>
/// Shared team inbox for WhatsApp. Outbound replies are stamped with the
/// sender's display name so the customer can tell who answered — the whole point
/// of the "lite" mode, and only possible because the reply goes through us.
/// </summary>
public class WhatsAppInboxService(
    FojiDbContext db,
    IEncryptionService encryption,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<WhatsAppInboxService> logger) : IWhatsAppInboxService
{
    /// <summary>Meta's customer service window. Free-form replies are rejected outside it.</summary>
    private static readonly TimeSpan ServiceWindow = TimeSpan.FromHours(24);

    private string GraphVersion => configuration["Meta:GraphVersion"] ?? "v25.0";

    // ── Inbound ───────────────────────────────────────────────────────────────

    public async Task RecordInboundAsync(
        int agentId, string phoneNumberId, string waId, string? profileName, string? wamId, string text)
    {
        // Meta retries webhooks and can batch up to 1000 updates — never store twice.
        if (!string.IsNullOrEmpty(wamId)
            && await db.WhatsAppMessages.AnyAsync(m => m.WamId == wamId))
        {
            logger.LogDebug("Duplicate WhatsApp message {WamId} — skipping", wamId);
            return;
        }

        var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == agentId)
            ?? throw new NotFoundException("Agent not found.");

        var now = DateTime.UtcNow;
        var conversation = await db.WhatsAppConversations
            .FirstOrDefaultAsync(c => c.AgentId == agentId && c.ContactWaId == waId);

        if (conversation == null)
        {
            conversation = new WhatsAppConversation
            {
                CompanyId = agent.CompanyId,
                AgentId = agentId,
                PhoneNumberId = phoneNumberId,
                ContactWaId = waId,
                ContactName = profileName,
                LastMessageAt = now,
                LastInboundAt = now,
                UnreadCount = 0,
            };
            db.WhatsAppConversations.Add(conversation);
        }
        else
        {
            // Trust the latest profile name; people change them.
            if (!string.IsNullOrWhiteSpace(profileName)) conversation.ContactName = profileName;
            conversation.PhoneNumberId = phoneNumberId;
            conversation.LastMessageAt = now;
            conversation.LastInboundAt = now;
        }

        conversation.UnreadCount++;

        db.WhatsAppMessages.Add(new WhatsAppMessage
        {
            CompanyId = agent.CompanyId,
            Conversation = conversation,
            Direction = MessageDirection.Inbound,
            Body = text,
            WamId = wamId,
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lost a race on the wamid or the conversation — the message is already recorded.
            logger.LogDebug("Concurrent insert for WhatsApp message {WamId} — ignoring", wamId);
            db.ChangeTracker.Clear();
        }
    }

    // ── Reads ─────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<InboxConversationItem>> GetConversationsAsync(int companyId, int? agentId = null)
    {
        var cutoff = DateTime.UtcNow - ServiceWindow;

        return await db.WhatsAppConversations
            .Where(c => c.CompanyId == companyId && (agentId == null || c.AgentId == agentId))
            .OrderByDescending(c => c.LastMessageAt)
            .Take(200)
            .Select(c => new InboxConversationItem(
                c.Id,
                c.AgentId,
                c.Agent.Name,
                c.ContactWaId,
                c.ContactName,
                c.ContactId,
                c.Messages.OrderByDescending(m => m.CreatedAt).Select(m => m.Body).FirstOrDefault(),
                c.LastMessageAt,
                c.LastInboundAt,
                c.UnreadCount,
                c.LastInboundAt != null && c.LastInboundAt > cutoff))
            .ToListAsync();
    }

    public async Task<InboxThreadResult?> GetThreadAsync(int companyId, int conversationId)
    {
        var cutoff = DateTime.UtcNow - ServiceWindow;

        var conversation = await db.WhatsAppConversations
            .Where(c => c.CompanyId == companyId && c.Id == conversationId)
            .Select(c => new InboxConversationItem(
                c.Id, c.AgentId, c.Agent.Name, c.ContactWaId, c.ContactName, c.ContactId,
                null, c.LastMessageAt, c.LastInboundAt, c.UnreadCount,
                c.LastInboundAt != null && c.LastInboundAt > cutoff))
            .FirstOrDefaultAsync();

        if (conversation == null) return null;

        var messages = await db.WhatsAppMessages
            .Where(m => m.ConversationId == conversationId && m.CompanyId == companyId)
            .OrderBy(m => m.CreatedAt)
            .Take(500)
            .Select(m => new InboxMessageItem(
                m.Id, m.Direction.ToString(), m.Body, m.SentByUserId, m.SenderDisplayName, m.CreatedAt))
            .ToListAsync();

        return new InboxThreadResult(conversation, messages);
    }

    public async Task MarkReadAsync(int companyId, int conversationId)
    {
        var conversation = await db.WhatsAppConversations
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Id == conversationId);
        if (conversation == null || conversation.UnreadCount == 0) return;
        conversation.UnreadCount = 0;
        await db.SaveChangesAsync();
    }

    // ── Outbound ──────────────────────────────────────────────────────────────

    public async Task<InboxMessageItem> SendReplyAsync(int companyId, int conversationId, int userId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("A message is required.");

        var conversation = await db.WhatsAppConversations
            .Include(c => c.Agent)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Id == conversationId)
            ?? throw new NotFoundException("Conversation not found.");

        // Meta rejects free-form messages outside the 24h window (error 131047);
        // sending anyway would fail silently from the user's point of view.
        if (conversation.LastInboundAt == null || conversation.LastInboundAt < DateTime.UtcNow - ServiceWindow)
            throw new DomainException(
                "This conversation is outside WhatsApp's 24-hour reply window. " +
                "The customer needs to message again before you can reply.");

        var displayName = await ResolveDisplayNameAsync(companyId, userId);
        var body = $"{displayName}:\n\n{text.Trim()}";

        var wamId = await SendViaMetaAsync(conversation, body);

        var message = new WhatsAppMessage
        {
            CompanyId = companyId,
            ConversationId = conversation.Id,
            Direction = MessageDirection.Outbound,
            Body = body,
            WamId = wamId,
            SentByUserId = userId,
            SenderDisplayName = displayName,
        };
        db.WhatsAppMessages.Add(message);

        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.UnreadCount = 0; // replying implies you've read it
        await db.SaveChangesAsync();

        return new InboxMessageItem(
            message.Id, message.Direction.ToString(), message.Body,
            message.SentByUserId, message.SenderDisplayName, message.CreatedAt);
    }

    /// <summary>Per-company display name, falling back to the user's first name.</summary>
    private async Task<string> ResolveDisplayNameAsync(int companyId, int userId)
    {
        var membership = await db.UserCompanies
            .Include(uc => uc.User)
            .FirstOrDefaultAsync(uc => uc.CompanyId == companyId && uc.UserId == userId)
            ?? throw new ForbiddenException();

        var custom = membership.WhatsAppDisplayName?.Trim();
        if (!string.IsNullOrWhiteSpace(custom)) return custom;

        var first = membership.User.FirstName?.Trim();
        return string.IsNullOrWhiteSpace(first) ? "Atendimento" : first;
    }

    private async Task<string?> SendViaMetaAsync(WhatsAppConversation conversation, string body)
    {
        var token = ResolveToken(conversation.Agent);
        if (string.IsNullOrEmpty(token))
            throw new DomainException("This agent has no WhatsApp access token configured.");

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var url = $"https://graph.facebook.com/{GraphVersion}/{conversation.PhoneNumberId}/messages";
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = conversation.ContactWaId,
            type = "text",
            text = new { preview_url = false, body },
        };

        var response = await client.PostAsJsonAsync(url, payload);
        var raw = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("WhatsApp send failed ({Status}): {Body}", (int)response.StatusCode, raw);
            throw new DomainException("WhatsApp rejected the message. Please try again.");
        }

        // Store the wamid so delivery-status webhooks can be matched later.
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.TryGetProperty("messages", out var messages)
                   && messages.ValueKind == JsonValueKind.Array
                   && messages.GetArrayLength() > 0
                   && messages[0].TryGetProperty("id", out var id)
                ? id.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private string? ResolveToken(Agent agent)
    {
        if (!string.IsNullOrEmpty(agent.WhatsAppAccessTokenEncrypted))
        {
            try
            {
                return encryption.Decrypt(agent.WhatsAppAccessTokenEncrypted);
            }
            catch
            {
                logger.LogError("Failed to decrypt WhatsApp token for agent {AgentId}", agent.Id);
            }
        }
        return configuration["Meta:WhatsAppToken"];
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}
