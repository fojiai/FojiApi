namespace FojiApi.Core.Entities;

/// <summary>Audit record of a deal moving between pipeline stages.</summary>
public class DealStageHistory : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int DealId { get; set; }
    public int? FromStageId { get; set; }
    public int ToStageId { get; set; }
    public int? ChangedByUserId { get; set; }

    // Navigation
    public Deal Deal { get; set; } = null!;
}
