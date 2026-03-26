using FojiApi.Core.Enums;

namespace FojiApi.Core.Entities;

public class AIModel : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public AiProvider Provider { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public decimal InputCostPer1M { get; set; }
    public decimal OutputCostPer1M { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
}
