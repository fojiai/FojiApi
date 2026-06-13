using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

/// <summary>
/// A CRM contact — the deduped person behind one or more raw <see cref="Lead"/> capture events.
/// Company-level (not agent-level): the same person talking to two agents is one contact.
/// </summary>
public class Contact : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    // Normalized dedup keys (email lowercased/trimmed; phone digits-only w/ country code).
    public string? EmailNormalized { get; set; }
    public string? PhoneNormalized { get; set; }

    public int? OwnerUserId { get; set; }
    public ContactStatus Status { get; set; } = ContactStatus.New;

    /// <summary>First-touch channel: "widget" | "whatsapp" | "manual" | "import".</summary>
    public string? Source { get; set; }

    public decimal? EstimatedValue { get; set; }
    public string? Notes { get; set; }

    /// <summary>Denormalized for cheap list sorting; bumped on any timeline event.</summary>
    public DateTime? LastActivityAt { get; set; }

    /// <summary>Set when an email/phone identity conflict was detected during upsert (needs human merge).</summary>
    public bool NeedsReviewDuplicate { get; set; } = false;

    // Navigation
    public Company Company { get; set; } = null!;
    public User? OwnerUser { get; set; }
    public ICollection<Lead> Leads { get; set; } = [];
    public ICollection<ContactTag> Tags { get; set; } = [];
    public ICollection<Deal> Deals { get; set; } = [];
}
