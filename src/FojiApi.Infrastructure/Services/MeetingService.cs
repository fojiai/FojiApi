using FojiApi.Core.Entities;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class MeetingService(FojiDbContext db, IContactService contactService) : IMeetingService
{
    public async Task<MeetingRecordedResult> RecordMeetingAsync(RecordMeetingInput input)
    {
        var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == input.AgentId)
            ?? throw new NotFoundException("Agent not found.");
        var companyId = agent.CompanyId;

        // Idempotency: a webhook/retry shouldn't create duplicate meetings for the same Google event.
        var existing = await db.Meetings.FirstOrDefaultAsync(m =>
            m.CompanyId == companyId && m.GoogleEventId == input.GoogleEventId);
        if (existing != null)
            return new MeetingRecordedResult(existing.Id, existing.ContactId);

        // Link to the deduped contact via the attendee identity (no Lead created).
        var contactId = await contactService.FindOrCreateContactAsync(
            companyId, input.AttendeeName, input.AttendeeEmail, null, "meeting");

        var meeting = new Meeting
        {
            CompanyId = companyId,
            AgentId = input.AgentId,
            ContactId = contactId,
            GoogleEventId = input.GoogleEventId,
            MeetLink = input.MeetLink,
            HtmlLink = input.HtmlLink,
            Title = input.Title,
            StartsAt = input.StartsAt,
            EndsAt = input.EndsAt,
            AttendeeEmail = input.AttendeeEmail,
            AttendeeName = input.AttendeeName,
        };
        db.Meetings.Add(meeting);

        // Follow-up task on the day of the meeting.
        db.CrmTasks.Add(new CrmTask
        {
            CompanyId = companyId,
            ContactId = contactId,
            Title = $"Meeting: {input.Title}",
            Type = CrmTaskType.Meeting,
            Priority = CrmTaskPriority.Normal,
            Status = CrmTaskStatus.Open,
            DueAt = input.StartsAt,
        });

        await db.SaveChangesAsync();
        return new MeetingRecordedResult(meeting.Id, contactId);
    }

    public async Task<IEnumerable<MeetingItem>> GetMeetingsAsync(int companyId, int? contactId = null)
    {
        var query = db.Meetings.Where(m => m.CompanyId == companyId);
        if (contactId.HasValue) query = query.Where(m => m.ContactId == contactId.Value);

        return await query
            .OrderByDescending(m => m.StartsAt)
            .Select(m => new MeetingItem(
                m.Id, m.ContactId, m.MeetLink, m.HtmlLink, m.Title,
                m.StartsAt, m.EndsAt, m.AttendeeEmail, m.AttendeeName))
            .ToListAsync();
    }
}
