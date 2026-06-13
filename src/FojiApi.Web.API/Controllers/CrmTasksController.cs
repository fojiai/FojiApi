using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

[Route("api/crm/tasks")]
public class CrmTasksController(
    ICrmTaskService taskService,
    IPlanEnforcementService planEnforcement,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet]
    public async Task<IActionResult> GetTasks(
        [FromQuery] int companyId,
        [FromQuery] int? assigneeUserId = null,
        [FromQuery] string? status = null,
        [FromQuery] int? contactId = null,
        [FromQuery] int? dealId = null)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        return Ok(await taskService.GetTasksAsync(companyId, assigneeUserId, ParseStatus(status), contactId, dealId));
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] UpsertTaskRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        return Ok(await taskService.CreateTaskAsync(req.CompanyId, ToInput(req)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateTask([FromRoute] int id, [FromBody] UpsertTaskRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        var updated = await taskService.UpdateTaskAsync(req.CompanyId, id, ToInput(req));
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> SetStatus([FromRoute] int id, [FromBody] SetTaskStatusRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        var status = ParseStatus(req.Status) ?? CrmTaskStatus.Open;
        var updated = await taskService.SetStatusAsync(req.CompanyId, id, status);
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTask([FromRoute] int id, [FromQuery] int companyId)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(companyId);
        return await taskService.DeleteTaskAsync(companyId, id) ? NoContent() : NotFound();
    }

    private static CrmTaskInput ToInput(UpsertTaskRequest req) => new(
        req.ContactId, req.DealId, req.Title, req.Description,
        ParseType(req.Type) ?? CrmTaskType.General,
        ParsePriority(req.Priority) ?? CrmTaskPriority.Normal,
        req.DueAt, req.AssigneeUserId);

    private static CrmTaskStatus? ParseStatus(string? s) =>
        Enum.TryParse<CrmTaskStatus>(s, true, out var v) ? v : null;
    private static CrmTaskType? ParseType(string? s) =>
        Enum.TryParse<CrmTaskType>(s, true, out var v) ? v : null;
    private static CrmTaskPriority? ParsePriority(string? s) =>
        Enum.TryParse<CrmTaskPriority>(s, true, out var v) ? v : null;

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}

public record UpsertTaskRequest(
    int CompanyId,
    int? ContactId,
    int? DealId,
    string Title,
    string? Description,
    string? Type,
    string? Priority,
    DateTime? DueAt,
    int? AssigneeUserId
);

public record SetTaskStatusRequest(int CompanyId, string Status);
