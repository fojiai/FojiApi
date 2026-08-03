using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class DealsController(
    IDealService dealService,
    IPlanEnforcementService planEnforcement,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    /// <summary>Kanban board — stages with their deals, for the company's default (or given) pipeline.</summary>
    [HttpGet("board")]
    public async Task<IActionResult> GetBoard([FromQuery] int companyId, [FromQuery] int? pipelineId = null)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(companyId);
        return Ok(await dealService.GetBoardAsync(companyId, pipelineId));
    }

    /// <summary>Single deal, for the detail view.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDeal([FromRoute] int id, [FromQuery] int companyId)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(companyId);
        var deal = await dealService.GetDealAsync(companyId, id);
        return deal == null ? NotFound() : Ok(deal);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDeal([FromBody] CreateDealRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        var created = await dealService.CreateDealAsync(req.CompanyId, new CreateDealInput(
            req.PipelineId, req.StageId, req.ContactId, req.OwnerUserId,
            req.Title, req.Value, req.Currency, req.ExpectedCloseDate), CurrentUser.UserId);
        return Ok(created);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDeal([FromRoute] int id, [FromQuery] int companyId)
    {
        EnsureCompanyAccess(companyId, CompanyRole.Admin);
        await planEnforcement.EnsureCanUseCrmAsync(companyId);
        return await dealService.DeleteDealAsync(companyId, id) ? NoContent() : NotFound();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateDeal([FromRoute] int id, [FromBody] UpdateDealRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        var updated = await dealService.UpdateDealAsync(req.CompanyId, id, new UpdateDealInput(
            req.OwnerUserId, req.Title, req.Value, req.Currency, req.ExpectedCloseDate));
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:int}/move")]
    public async Task<IActionResult> MoveDeal([FromRoute] int id, [FromBody] MoveDealRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        var moved = await dealService.MoveStageAsync(req.CompanyId, id, req.ToStageId, CurrentUser.UserId);
        return moved == null ? NotFound() : Ok(moved);
    }

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}

public record CreateDealRequest(
    int CompanyId,
    int? PipelineId,
    int? StageId,
    int ContactId,
    int? OwnerUserId,
    string Title,
    decimal Value,
    string? Currency,
    DateTime? ExpectedCloseDate
);

public record UpdateDealRequest(
    int CompanyId,
    int? OwnerUserId,
    string Title,
    decimal Value,
    string? Currency,
    DateTime? ExpectedCloseDate
);

public record MoveDealRequest(int CompanyId, int ToStageId);
