namespace FojiApi.Core.Interfaces.Services;

public interface ICrmEmailService
{
    Task<EmailLogItem> SendAsync(int companyId, int? sentByUserId, SendCrmEmailInput input);
    Task<IEnumerable<EmailLogItem>> GetForContactAsync(int companyId, int contactId);

    /// <summary>Generate an AI-drafted subject + body for a proposal/follow-up email (via foji-ai-api).</summary>
    Task<EmailDraft> DraftAsync(int companyId, DraftEmailRequest request);
}

public record DraftEmailRequest(int? ContactId, string Goal, string? Tone);

public record EmailDraft(string Subject, string Body);

public record SendCrmEmailInput(int ContactId, int? DealId, string ToEmail, string Subject, string Body);

public record EmailLogItem(
    int Id,
    int? ContactId,
    int? DealId,
    string ToEmail,
    string Subject,
    string Body,
    DateTime SentAt
);
