using System.Security.Cryptography;
using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Services;

public class AgentService(
    FojiDbContext db,
    IIndustryPromptService industryPromptService,
    IPlanEnforcementService planEnforcement,
    IEncryptionService encryption,
    ILogger<AgentService> logger) : IAgentService
{
    public async Task<IEnumerable<AgentListItem>> GetAgentsAsync(int companyId)
    {
        return await db.Agents
            .Where(a => a.CompanyId == companyId)
            .Select(a => new AgentListItem(
                a.Id, a.Name, a.Description, a.IsActive,
                a.IndustryType.ToString(), a.AgentLanguage.ToString(),
                a.AgentToken, a.WhatsAppEnabled,
                a.Files.Count(f => f.ProcessingStatus == FileProcessingStatus.Ready),
                a.CreatedAt))
            .ToListAsync();
    }

    public async Task<AgentDetail> GetAgentAsync(int agentId)
    {
        var agent = await db.Agents
            .Include(a => a.Files)
            .Include(a => a.CalendarConnection)
            .FirstOrDefaultAsync(a => a.Id == agentId)
            ?? throw new NotFoundException("Agent not found.");

        var calendarActive = agent.CalendarConnection is { IsActive: true };

        return new AgentDetail(
            agent.Id, agent.Name, agent.Description, agent.IsActive,
            agent.IndustryType.ToString(), agent.SystemPrompt, agent.UserPrompt,
            agent.AgentLanguage.ToString(), agent.AgentToken,
            agent.WhatsAppEnabled, agent.WhatsAppPhoneNumberId,
            agent.CompanyId, agent.CreatedAt, agent.UpdatedAt,
            agent.Files.Select(f => new AgentFileItem(
                f.Id, f.FileName, f.FileSizeBytes, f.ProcessingStatus.ToString(), f.CreatedAt)),
            agent.SupportWhatsAppNumber, agent.SalesWhatsAppNumber,
            agent.SupportEmail, agent.SalesEmail,
            agent.WelcomeMessage, agent.ConversationStarters,
            agent.WidgetPrimaryColor, agent.WidgetTitle,
            agent.WidgetPlaceholder, agent.WidgetPosition,
            agent.ResponseStyle,
            agent.LeadCaptureEnabled, agent.LeadCapturePrompt,
            agent.HandoffEnabled, agent.HandoffNotifyEmail,
            agent.HandoffNotifyWhatsApp, agent.HandoffMessage,
            calendarActive,
            calendarActive ? agent.CalendarConnection!.GoogleAccountEmail : null,
            !string.IsNullOrEmpty(agent.WhatsAppAccessTokenEncrypted),
            agent.WhatsAppMode.ToString()
        );
    }

    public async Task<AgentCreatedResult> CreateAgentAsync(
        int companyId, string name, string? description, string industryType, string? agentLanguage, string? userPrompt)
    {
        await planEnforcement.EnsureCanCreateAgentAsync(companyId);

        if (!Enum.TryParse<IndustryType>(industryType.Replace("_", ""), true, out var parsedIndustry))
            throw new DomainException($"Invalid industry type: {industryType}. Valid values: accounting_finance, law, internal_systems.");

        if (!Enum.TryParse<AgentLanguage>((agentLanguage ?? "pt-br").Replace("-", ""), true, out var parsedLanguage))
            throw new DomainException($"Invalid agent language: {agentLanguage}. Valid values: pt-br, en, es.");

        var company = await db.Companies.FindAsync(companyId)
            ?? throw new NotFoundException("Company not found.");

        var systemPrompt = industryPromptService.GetSystemPrompt(parsedIndustry, company.Name, parsedLanguage);

        var agent = new Agent
        {
            CompanyId = companyId,
            Name = name.Trim(),
            Description = description?.Trim(),
            IndustryType = parsedIndustry,
            AgentLanguage = parsedLanguage,
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt?.Trim(),
            AgentToken = GenerateHexToken()
        };

        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        return new AgentCreatedResult(agent.Id, agent.Name, agent.AgentToken);
    }

    public async Task<AgentUpdatedResult> UpdateAgentAsync(
        int agentId, string? name, string? description, string? userPrompt,
        bool? isActive, string? agentLanguage, bool? whatsAppEnabled, string? whatsAppPhoneNumberId,
        string? supportWhatsAppNumber, string? salesWhatsAppNumber, string? supportEmail, string? salesEmail,
        string? welcomeMessage, string? conversationStarters,
        string? widgetPrimaryColor, string? widgetTitle, string? widgetPlaceholder, string? widgetPosition,
        string? responseStyle, bool? leadCaptureEnabled, string? leadCapturePrompt,
        bool? handoffEnabled, string? handoffNotifyEmail, string? handoffNotifyWhatsApp, string? handoffMessage,
        string? whatsAppAccessToken = null, string? whatsAppMode = null)
    {
        var agent = await db.Agents.FindAsync(agentId)
            ?? throw new NotFoundException("Agent not found.");

        if (name != null) agent.Name = name.Trim();
        if (description != null) agent.Description = description.Trim();
        if (userPrompt != null) agent.UserPrompt = userPrompt.Trim();
        if (isActive.HasValue) agent.IsActive = isActive.Value;

        if (agentLanguage != null)
        {
            if (!Enum.TryParse<AgentLanguage>(agentLanguage.Replace("-", ""), true, out var lang))
                throw new DomainException($"Invalid agent language: {agentLanguage}. Valid values: pt-br, en, es.");
            agent.AgentLanguage = lang;
        }

        if (whatsAppEnabled.HasValue)
        {
            if (whatsAppEnabled.Value)
                await planEnforcement.EnsureCanEnableWhatsAppAsync(agent.CompanyId);
            agent.WhatsAppEnabled = whatsAppEnabled.Value;
        }

        if (whatsAppPhoneNumberId != null) agent.WhatsAppPhoneNumberId = whatsAppPhoneNumberId;

        if (whatsAppMode != null)
        {
            if (!Enum.TryParse<WhatsAppMode>(whatsAppMode, true, out var mode))
                throw new DomainException($"Invalid WhatsApp mode: {whatsAppMode}. Valid values: Agent, Inbox.");
            agent.WhatsAppMode = mode;
        }

        // WhatsApp access token — encrypt at rest; empty string clears it. Never returned to clients.
        if (whatsAppAccessToken != null)
        {
            try
            {
                agent.WhatsAppAccessTokenEncrypted = whatsAppAccessToken.Trim().Length > 0
                    ? encryption.Encrypt(whatsAppAccessToken.Trim())
                    : null;
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException)
            {
                // A missing or malformed GoogleCalendar:EncryptionKey is a server
                // misconfiguration, not a crash. Surfaced as a 400 with a real
                // sentence so it doesn't read as "unexpected error occurred".
                logger.LogError(ex, "Cannot encrypt the WhatsApp access token for agent {AgentId} — check Encryption:Key", agentId);
                throw new DomainException(
                    "The WhatsApp access token could not be stored: this server's encryption key is missing or invalid. "
                    + "An administrator must configure Encryption:Key (base64, 32 bytes).");
            }
        }

        // Escalation contacts — plan-gated, only enforce when any are being set
        var settingEscalation = supportWhatsAppNumber != null || salesWhatsAppNumber != null
                             || supportEmail != null || salesEmail != null;
        if (settingEscalation)
            await planEnforcement.EnsureCanUseEscalationContactsAsync(agent.CompanyId);

        // Allow clearing individual contacts by passing empty string
        if (supportWhatsAppNumber != null)
            agent.SupportWhatsAppNumber = supportWhatsAppNumber.Trim().Length > 0 ? supportWhatsAppNumber.Trim() : null;
        if (salesWhatsAppNumber != null)
            agent.SalesWhatsAppNumber = salesWhatsAppNumber.Trim().Length > 0 ? salesWhatsAppNumber.Trim() : null;
        if (supportEmail != null)
            agent.SupportEmail = supportEmail.Trim().Length > 0 ? supportEmail.Trim() : null;
        if (salesEmail != null)
            agent.SalesEmail = salesEmail.Trim().Length > 0 ? salesEmail.Trim() : null;

        // Widget customization
        if (welcomeMessage != null)
            agent.WelcomeMessage = welcomeMessage.Trim().Length > 0 ? welcomeMessage.Trim() : null;
        if (conversationStarters != null)
            agent.ConversationStarters = conversationStarters.Trim().Length > 0 ? conversationStarters.Trim() : null;
        if (widgetPrimaryColor != null)
            agent.WidgetPrimaryColor = widgetPrimaryColor.Trim().Length > 0 ? widgetPrimaryColor.Trim() : null;
        if (widgetTitle != null)
            agent.WidgetTitle = widgetTitle.Trim().Length > 0 ? widgetTitle.Trim() : null;
        if (widgetPlaceholder != null)
            agent.WidgetPlaceholder = widgetPlaceholder.Trim().Length > 0 ? widgetPlaceholder.Trim() : null;
        if (widgetPosition != null)
            agent.WidgetPosition = widgetPosition.Trim().Length > 0 ? widgetPosition.Trim() : null;

        if (responseStyle != null)
        {
            var valid = new[] { "Professional", "Friendly", "Concise" };
            if (!valid.Contains(responseStyle))
                throw new DomainException($"Invalid response style: {responseStyle}. Valid values: Professional, Friendly, Concise.");
            agent.ResponseStyle = responseStyle;
        }

        if (leadCaptureEnabled.HasValue) agent.LeadCaptureEnabled = leadCaptureEnabled.Value;
        if (leadCapturePrompt != null)
            agent.LeadCapturePrompt = leadCapturePrompt.Trim().Length > 0 ? leadCapturePrompt.Trim() : null;

        if (handoffEnabled.HasValue) agent.HandoffEnabled = handoffEnabled.Value;
        if (handoffNotifyEmail != null)
            agent.HandoffNotifyEmail = handoffNotifyEmail.Trim().Length > 0 ? handoffNotifyEmail.Trim() : null;
        if (handoffNotifyWhatsApp != null)
            agent.HandoffNotifyWhatsApp = handoffNotifyWhatsApp.Trim().Length > 0 ? handoffNotifyWhatsApp.Trim() : null;
        if (handoffMessage != null)
            agent.HandoffMessage = handoffMessage.Trim().Length > 0 ? handoffMessage.Trim() : null;

        await db.SaveChangesAsync();
        return new AgentUpdatedResult(agent.Id, agent.Name, agent.IsActive, agent.UpdatedAt);
    }

    public async Task DeleteAgentAsync(int agentId)
    {
        var agent = await db.Agents.FindAsync(agentId)
            ?? throw new NotFoundException("Agent not found.");
        db.Agents.Remove(agent);
        await db.SaveChangesAsync();
    }

    public async Task<string> RegenerateTokenAsync(int agentId)
    {
        var agent = await db.Agents.FindAsync(agentId)
            ?? throw new NotFoundException("Agent not found.");
        agent.AgentToken = GenerateHexToken();
        await db.SaveChangesAsync();
        return agent.AgentToken;
    }

    public async Task<EmbedCodeResult> GetEmbedCodeAsync(int agentId, string? widgetBaseUrl)
    {
        var agent = await db.Agents.FindAsync(agentId)
            ?? throw new NotFoundException("Agent not found.");

        var baseUrl = widgetBaseUrl ?? "https://widget.foji.ai";
        var embedCode = $"""
            <iframe
              src="{baseUrl}/?agent_token={agent.AgentToken}&dark=auto"
              width="100%"
              height="600px"
              style="border: none; border-radius: 8px;"
              loading="lazy">
            </iframe>
            """;

        return new EmbedCodeResult(embedCode, agent.AgentToken, baseUrl);
    }

    private static string GenerateHexToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();
}
