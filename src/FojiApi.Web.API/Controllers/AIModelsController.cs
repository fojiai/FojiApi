using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class AIModelsController(IAIModelService aiModelService, ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet]
    public async Task<IActionResult> GetModels()
        => Ok(await aiModelService.GetModelsAsync(CurrentUser.IsSuperAdmin));

    [HttpPost]
    public async Task<IActionResult> CreateModel([FromBody] CreateAIModelRequest req)
    {
        if (!CurrentUser.IsSuperAdmin) throw new ForbiddenException();
        var result = await aiModelService.CreateModelAsync(req.Name, req.DisplayName, req.Provider, req.ModelId, req.InputCostPer1M, req.OutputCostPer1M, req.IsActive, req.IsDefault);
        return CreatedAtAction(nameof(GetModels), new { }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateModel(int id, [FromBody] UpdateAIModelRequest req)
    {
        if (!CurrentUser.IsSuperAdmin) throw new ForbiddenException();
        return Ok(await aiModelService.UpdateModelAsync(id, req.DisplayName, req.IsActive, req.IsDefault, req.InputCostPer1M, req.OutputCostPer1M));
    }
}

public record CreateAIModelRequest(string Name, string DisplayName, string Provider, string ModelId, decimal InputCostPer1M, decimal OutputCostPer1M, bool IsActive = true, bool IsDefault = false);
public record UpdateAIModelRequest(string? DisplayName, bool? IsActive, bool? IsDefault, decimal? InputCostPer1M, decimal? OutputCostPer1M);
