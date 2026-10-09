namespace FojiApi.Core.Entities;

/// <summary>
/// An Asaas webhook, stored before we answer 200 and processed in the background
/// (Asaas waits only 10s and pauses the queue after 15 failures). The Asaas event
/// id is unique, so redeliveries are ignored.
/// </summary>
public class BillingWebhookEvent
{
    public long Id { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? LastError { get; set; }
}
