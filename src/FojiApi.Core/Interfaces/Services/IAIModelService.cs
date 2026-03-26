namespace FojiApi.Core.Interfaces.Services;

public interface IAIModelService
{
    Task<IEnumerable<AIModelResult>> GetModelsAsync(bool superAdmin);
    Task<AIModelResult> CreateModelAsync(string name, string displayName, string provider, string modelId, decimal inputCostPer1M, decimal outputCostPer1M, bool isActive, bool isDefault);
    Task<AIModelResult> UpdateModelAsync(int id, string? displayName, bool? isActive, bool? isDefault, decimal? inputCostPer1M, decimal? outputCostPer1M);
}

public record AIModelResult(int Id, string Name, string DisplayName, string Provider, string ModelId, decimal InputCostPer1M, decimal OutputCostPer1M, bool IsActive, bool IsDefault);
