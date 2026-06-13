using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class PipelinesController(
    IPipelineService pipelineService,
    IPlanEnforcementService planEnforcement,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet]
    public async Task<IActionResult> GetPipelines([FromQuery] int companyId)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        return Ok(await pipelineService.GetPipelinesAsync(companyId));
    }

    /// <summary>Returns the company's default pipeline, creating it on first call.</summary>
    [HttpGet("default")]
    public async Task<IActionResult> GetDefaultPipeline([FromQuery] int companyId)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(companyId);
        return Ok(await pipelineService.EnsureDefaultPipelineAsync(companyId));
    }

    [HttpPost("{pipelineId:int}/stages")]
    public async Task<IActionResult> AddStage([FromRoute] int pipelineId, [FromBody] AddStageRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.Admin);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        return Ok(await pipelineService.AddStageAsync(req.CompanyId, pipelineId, req.Name, req.IsWon, req.IsLost));
    }

    [HttpPut("stages/{stageId:int}")]
    public async Task<IActionResult> UpdateStage([FromRoute] int stageId, [FromBody] UpdateStageRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.Admin);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        var updated = await pipelineService.UpdateStageAsync(req.CompanyId, stageId, req.Name, req.IsWon, req.IsLost);
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpDelete("stages/{stageId:int}")]
    public async Task<IActionResult> RemoveStage([FromRoute] int stageId, [FromQuery] int companyId)
    {
        EnsureCompanyAccess(companyId, CompanyRole.Admin);
        await planEnforcement.EnsureCanUseCrmAsync(companyId);
        await pipelineService.RemoveStageAsync(companyId, stageId);
        return NoContent();
    }

    [HttpPost("{pipelineId:int}/reorder")]
    public async Task<IActionResult> ReorderStages([FromRoute] int pipelineId, [FromBody] ReorderStagesRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.Admin);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        await pipelineService.ReorderStagesAsync(req.CompanyId, pipelineId, req.OrderedStageIds);
        return NoContent();
    }

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}

public record AddStageRequest(int CompanyId, string Name, bool IsWon, bool IsLost);
public record UpdateStageRequest(int CompanyId, string Name, bool IsWon, bool IsLost);
public record ReorderStagesRequest(int CompanyId, List<int> OrderedStageIds);
