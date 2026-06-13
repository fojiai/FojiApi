namespace FojiApi.Core.Entities;

/// <summary>A scheduled meeting (Google Calendar event), optionally with a Google Meet link.</summary>
public class Meeting : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int AgentId { get; set; }
    public int? ContactId { get; set; }
    public int? DealId { get; set; }

    public string GoogleEventId { get; set; } = string.Empty;
    public string? MeetLink { get; set; }
    public string? HtmlLink { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? AttendeeEmail { get; set; }
    public string? AttendeeName { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public Contact? Contact { get; set; }
}
