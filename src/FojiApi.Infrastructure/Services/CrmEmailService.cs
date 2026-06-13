using FojiApi.Core.Entities;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class CrmEmailService(FojiDbContext db, IEmailService emailService) : ICrmEmailService
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
}
