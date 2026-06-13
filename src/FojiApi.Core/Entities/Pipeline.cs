namespace FojiApi.Core.Entities;

/// <summary>A sales pipeline owned by a company. Each company has one default pipeline (seeded on first use).</summary>
public class Pipeline : BaseEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
    public int SortOrder { get; set; }

    // Navigation
    public Company Company { get; set; } = null!;
    public ICollection<PipelineStage> Stages { get; set; } = [];
}
