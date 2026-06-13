namespace FojiApi.Core.Entities;

/// <summary>An ordered column within a <see cref="Pipeline"/> (e.g. New, Contacted, Proposal, Won, Lost).</summary>
public class PipelineStage : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int PipelineId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    /// <summary>Terminal-won stage marker — deals moved here become Status=Won.</summary>
    public bool IsWon { get; set; } = false;

    /// <summary>Terminal-lost stage marker — deals moved here become Status=Lost.</summary>
    public bool IsLost { get; set; } = false;

    // Navigation
    public Pipeline Pipeline { get; set; } = null!;
}
