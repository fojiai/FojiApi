namespace FojiApi.Core.Entities;

/// <summary>A record of an outbound CRM email (proposal / follow-up) sent to a contact via Resend.</summary>
public class EmailLog : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? ContactId { get; set; }
    public int? DealId { get; set; }
    public int? SentByUserId { get; set; }

    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public Contact? Contact { get; set; }
}
