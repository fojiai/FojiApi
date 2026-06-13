namespace FojiApi.Core.Interfaces.Services;

public interface ICrmEmailService
{
    Task<EmailLogItem> SendAsync(int companyId, int? sentByUserId, SendCrmEmailInput input);
    Task<IEnumerable<EmailLogItem>> GetForContactAsync(int companyId, int contactId);
}

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
