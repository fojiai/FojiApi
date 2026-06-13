using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

/// <summary>A sales opportunity tied to a contact, moving through a pipeline's stages.</summary>
public class Deal : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int PipelineId { get; set; }
    public int StageId { get; set; }
    public int ContactId { get; set; }
    public int? OwnerUserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Currency { get; set; } = "BRL";

    /// <summary>Denormalized from the current stage's IsWon/IsLost markers.</summary>
    public DealStatus Status { get; set; } = DealStatus.Open;

    public DateTime? ExpectedCloseDate { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public Contact Contact { get; set; } = null!;
    public Pipeline Pipeline { get; set; } = null!;
    public PipelineStage Stage { get; set; } = null!;
    public User? OwnerUser { get; set; }
    public ICollection<DealStageHistory> StageHistory { get; set; } = [];
}
