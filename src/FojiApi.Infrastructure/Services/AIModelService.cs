using FojiApi.Core.Enums;
using FojiApi.Core.Entities;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class AIModelService(FojiDbContext db) : IAIModelService
{
    public async Task<IEnumerable<AIModelResult>> GetModelsAsync(bool superAdmin)
    {
        var query = db.AIModels.AsQueryable();
        if (!superAdmin) query = query.Where(m => m.IsActive);

        return await query
            .OrderBy(m => m.Provider).ThenBy(m => m.Name)
            .Select(m => new AIModelResult(
                m.Id, m.Name, m.DisplayName, m.Provider.ToString(),
                m.ModelId, m.InputCostPer1M, m.OutputCostPer1M, m.IsActive, m.IsDefault))
            .ToListAsync();
    }

    public async Task<AIModelResult> CreateModelAsync(
        string name, string displayName, string provider, string modelId,
        decimal inputCostPer1M, decimal outputCostPer1M, bool isActive, bool isDefault)
    {
        if (!Enum.TryParse<AiProvider>(provider, true, out var parsedProvider))
            throw new DomainException($"Invalid provider: {provider}. Valid values: OpenAi, Gemini, Bedrock.");

        var model = new AIModel
        {
            Name = name.Trim(),
            DisplayName = displayName.Trim(),
            Provider = parsedProvider,
            ModelId = modelId.Trim(),
            InputCostPer1M = inputCostPer1M,
            OutputCostPer1M = outputCostPer1M,
            IsActive = isActive,
            IsDefault = isDefault
        };

        db.AIModels.Add(model);
        await db.SaveChangesAsync();

        return new AIModelResult(model.Id, model.Name, model.DisplayName, model.Provider.ToString(), model.ModelId, model.InputCostPer1M, model.OutputCostPer1M, model.IsActive, model.IsDefault);
    }

    public async Task<AIModelResult> UpdateModelAsync(
        int id, string? displayName, bool? isActive, bool? isDefault,
        decimal? inputCostPer1M, decimal? outputCostPer1M)
    {
        var model = await db.AIModels.FindAsync(id)
            ?? throw new NotFoundException("AI model not found.");

        if (displayName != null) model.DisplayName = displayName.Trim();
        if (isActive.HasValue) model.IsActive = isActive.Value;
        if (isDefault.HasValue) model.IsDefault = isDefault.Value;
        if (inputCostPer1M.HasValue) model.InputCostPer1M = inputCostPer1M.Value;
        if (outputCostPer1M.HasValue) model.OutputCostPer1M = outputCostPer1M.Value;

        await db.SaveChangesAsync();
        return new AIModelResult(model.Id, model.Name, model.DisplayName, model.Provider.ToString(), model.ModelId, model.InputCostPer1M, model.OutputCostPer1M, model.IsActive, model.IsDefault);
    }
}
