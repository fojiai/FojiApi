using FojiApi.Core.Enums;

namespace FojiApi.Core.Interfaces.Services;

public interface IContactService
{
    /// <summary>
    /// Internal capture path: writes the raw Lead and find-or-creates the deduped Contact in one flow.
    /// Called by foji-ai-api via the internal endpoint after the widget/WhatsApp lead form.
    /// </summary>
    Task<ContactCaptureResult> CaptureLeadAndUpsertContactAsync(
        int agentId, string sessionId, string? name, string? email, string? phone, string source);

    Task<IEnumerable<ContactListItem>> GetContactsAsync(
        int companyId, int? ownerUserId = null, ContactStatus? status = null, string? tag = null, string? search = null);

    Task<ContactDetail?> GetContactAsync(int companyId, int contactId);
    Task<IEnumerable<TimelineItem>> GetTimelineAsync(int companyId, int contactId);
    Task<ContactDetail> CreateContactAsync(int companyId, ContactInput input);
    Task<ContactDetail?> UpdateContactAsync(int companyId, int contactId, ContactInput input);
    Task<IReadOnlyList<string>> AddTagAsync(int companyId, int contactId, string tag);
    Task<IReadOnlyList<string>> RemoveTagAsync(int companyId, int contactId, string tag);
    Task<int> GetContactCountAsync(int companyId);
}

public record ContactCaptureResult(int LeadId, int? ContactId, string SessionId);

public record ContactListItem(
    int Id,
    string? Name,
    string? Email,
    string? Phone,
    string Status,
    string? Source,
    int? OwnerUserId,
    string? OwnerName,
    decimal? EstimatedValue,
    bool NeedsReviewDuplicate,
    DateTime? LastActivityAt,
    IReadOnlyList<string> Tags,
    int LeadCount,
    int DealCount,
    DateTime CreatedAt
);

public record ContactDetail(
    int Id,
    string? Name,
    string? Email,
    string? Phone,
    string Status,
    string? Source,
    int? OwnerUserId,
    string? OwnerName,
    decimal? EstimatedValue,
    string? Notes,
    bool NeedsReviewDuplicate,
    DateTime? LastActivityAt,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt
);

public record ContactInput(
    string? Name,
    string? Email,
    string? Phone,
    int? OwnerUserId,
    ContactStatus Status,
    string? Source,
    decimal? EstimatedValue,
    string? Notes
);

/// <summary>A single merged event on a contact's activity timeline.</summary>
public record TimelineItem(
    string Type,        // "lead" | "handoff" | "deal_created" | "deal_stage" | "deal_won" | "deal_lost"
    DateTime Timestamp,
    string Title,
    string? Detail,
    int? RefId
);
