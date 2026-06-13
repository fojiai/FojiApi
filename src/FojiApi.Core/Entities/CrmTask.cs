using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

/// <summary>A follow-up task in the CRM, optionally linked to a contact and/or deal.</summary>
public class CrmTask : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? ContactId { get; set; }
    public int? DealId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CrmTaskType Type { get; set; } = CrmTaskType.General;
    public CrmTaskPriority Priority { get; set; } = CrmTaskPriority.Normal;
    public CrmTaskStatus Status { get; set; } = CrmTaskStatus.Open;

    public DateTime? DueAt { get; set; }
    public int? AssigneeUserId { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public Contact? Contact { get; set; }
    public Deal? Deal { get; set; }
    public User? Assignee { get; set; }
}
