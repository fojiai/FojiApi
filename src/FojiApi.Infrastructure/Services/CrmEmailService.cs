using System.Net.Http.Json;
using FojiApi.Core.Entities;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FojiApi.Infrastructure.Services;

public class CrmEmailService(
    FojiDbContext db,
    IEmailService emailService,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : ICrmEmailService
{
    public async Task<EmailLogItem> SendAsync(int companyId, int? sentByUserId, SendCrmEmailInput input)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Id == input.ContactId)
            ?? throw new NotFoundException("Contact not found.");

        if (input.DealId != null && !await db.Deals.AnyAsync(d => d.CompanyId == companyId && d.Id == input.DealId))
            throw new NotFoundException("Deal not found.");

        var to = input.ToEmail.Trim();
        if (string.IsNullOrWhiteSpace(to)) throw new DomainException("A recipient email is required.");
        if (string.IsNullOrWhiteSpace(input.Subject)) throw new DomainException("A subject is required.");
        if (string.IsNullOrWhiteSpace(input.Body)) throw new DomainException("An email body is required.");

        await emailService.SendCrmEmailAsync(to, input.Subject.Trim(), input.Body);

        var now = DateTime.UtcNow;
        var log = new EmailLog
        {
            CompanyId = companyId,
            ContactId = input.ContactId,
            DealId = input.DealId,
            SentByUserId = sentByUserId,
            ToEmail = to,
            Subject = input.Subject.Trim(),
            Body = input.Body,
            SentAt = now,
        };
        db.EmailLogs.Add(log);
        contact.LastActivityAt = now;
        await db.SaveChangesAsync();

        return new EmailLogItem(log.Id, log.ContactId, log.DealId, log.ToEmail, log.Subject, log.Body, log.SentAt);
    }

    public async Task<IEnumerable<EmailLogItem>> GetForContactAsync(int companyId, int contactId)
    {
        return await db.EmailLogs
            .Where(e => e.CompanyId == companyId && e.ContactId == contactId)
            .OrderByDescending(e => e.SentAt)
            .Select(e => new EmailLogItem(e.Id, e.ContactId, e.DealId, e.ToEmail, e.Subject, e.Body, e.SentAt))
            .ToListAsync();
    }

    public async Task<EmailDraft> DraftAsync(int companyId, DraftEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Goal))
            throw new DomainException("Describe what the email should accomplish.");

        // Any active agent of the company gives us a valid token to authenticate the AI request.
        var agentToken = await db.Agents
            .Where(a => a.CompanyId == companyId && a.IsActive)
            .OrderBy(a => a.Id)
            .Select(a => a.AgentToken)
            .FirstOrDefaultAsync()
            ?? throw new DomainException("Create an agent first to use AI drafting.");

        string? contactName = null;
        if (request.ContactId != null)
            contactName = await db.Contacts
                .Where(c => c.CompanyId == companyId && c.Id == request.ContactId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync();

        var baseUrl = configuration["AiApi:BaseUrl"]?.TrimEnd('/')
            ?? throw new DomainException("AI service is not configured.");
        var internalKey = configuration["InternalApiKey"] ?? "";

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v1/internal/crm/draft-email")
        {
            Content = JsonContent.Create(new
            {
                agent_token = agentToken,
                contact_name = contactName,
                goal = request.Goal,
                tone = request.Tone,
            }),
        };
        req.Headers.Add("X-Internal-Key", internalKey);

        HttpResponseMessage resp;
        try
        {
            resp = await client.SendAsync(req);
        }
        catch (Exception)
        {
            throw new DomainException("The AI drafting service is temporarily unavailable. Please try again.");
        }

        if (!resp.IsSuccessStatusCode)
            throw new DomainException("Could not generate a draft right now. Please try again.");

        var draft = await resp.Content.ReadFromJsonAsync<AiDraftResponse>();
        if (draft == null)
            throw new DomainException("The AI service returned an unexpected response.");

        return new EmailDraft(draft.subject, draft.body);
    }

    private sealed record AiDraftResponse(string subject, string body);
}
