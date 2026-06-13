namespace FojiApi.Core.Interfaces.Services;

public interface IMeetingService
{
    /// <summary>
    /// Records a booked Google Calendar meeting, links it to the deduped contact (by attendee email),
    /// and creates a follow-up task. Called by foji-ai-api after it creates the calendar event.
    /// </summary>
    Task<MeetingRecordedResult> RecordMeetingAsync(RecordMeetingInput input);

    Task<IEnumerable<MeetingItem>> GetMeetingsAsync(int companyId, int? contactId = null);
}

public record RecordMeetingInput(
    int AgentId,
    string GoogleEventId,
    string? MeetLink,
    string? HtmlLink,
    string Title,
    DateTime StartsAt,
    DateTime EndsAt,
    string? AttendeeEmail,
    string? AttendeeName
);

public record MeetingRecordedResult(int MeetingId, int? ContactId);

public record MeetingItem(
    int Id,
    int? ContactId,
    string? MeetLink,
    string? HtmlLink,
    string Title,
    DateTime StartsAt,
    DateTime EndsAt,
    string? AttendeeEmail,
    string? AttendeeName
);
