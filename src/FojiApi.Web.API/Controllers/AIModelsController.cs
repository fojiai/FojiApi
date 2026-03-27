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

public record CreateAIModelRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(100)]
    string Name,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(100)]
    string DisplayName,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(50)]
    string Provider,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(100)]
    string ModelId,

    [property: System.ComponentModel.DataAnnotations.Range(0, 10000)]
    decimal InputCostPer1M,

    [property: System.ComponentModel.DataAnnotations.Range(0, 10000)]
    decimal OutputCostPer1M,

    bool IsActive = true,
    bool IsDefault = false
);

public record UpdateAIModelRequest(
    [property: System.ComponentModel.DataAnnotations.StringLength(100)]
    string? DisplayName,
    bool? IsActive,
    bool? IsDefault,

    [property: System.ComponentModel.DataAnnotations.Range(0, 10000)]
    decimal? InputCostPer1M,

    [property: System.ComponentModel.DataAnnotations.Range(0, 10000)]
    decimal? OutputCostPer1M
);
