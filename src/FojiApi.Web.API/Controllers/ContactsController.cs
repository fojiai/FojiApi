using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class ContactsController(
    IContactService contactService,
    IPlanEnforcementService planEnforcement,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet]
    public async Task<IActionResult> GetContacts(
        [FromQuery] int companyId,
        [FromQuery] int? ownerUserId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? tag = null,
        [FromQuery] string? search = null)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(companyId);
        return Ok(await contactService.GetContactsAsync(companyId, ownerUserId, ParseStatus(status), tag, search));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetContact([FromRoute] int id, [FromQuery] int companyId)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(companyId);
        var contact = await contactService.GetContactAsync(companyId, id);
        return contact == null ? NotFound() : Ok(contact);
    }

    [HttpGet("{id:int}/timeline")]
    public async Task<IActionResult> GetTimeline([FromRoute] int id, [FromQuery] int companyId)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(companyId);
        return Ok(await contactService.GetTimelineAsync(companyId, id));
    }

    [HttpPost]
    public async Task<IActionResult> CreateContact([FromBody] UpsertContactRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        var created = await contactService.CreateContactAsync(req.CompanyId, ToInput(req));
        return CreatedAtAction(nameof(GetContact), new { id = created.Id, companyId = req.CompanyId }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateContact([FromRoute] int id, [FromBody] UpsertContactRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        var updated = await contactService.UpdateContactAsync(req.CompanyId, id, ToInput(req));
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpPost("{id:int}/tags")]
    public async Task<IActionResult> AddTag([FromRoute] int id, [FromBody] TagRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        return Ok(await contactService.AddTagAsync(req.CompanyId, id, req.Tag));
    }

    [HttpDelete("{id:int}/tags")]
    public async Task<IActionResult> RemoveTag([FromRoute] int id, [FromQuery] int companyId, [FromQuery] string tag)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(companyId);
        return Ok(await contactService.RemoveTagAsync(companyId, id, tag));
    }

    private static ContactInput ToInput(UpsertContactRequest req) => new(
        req.Name, req.Email, req.Phone, req.OwnerUserId,
        ParseStatus(req.Status) ?? ContactStatus.New, req.Source, req.EstimatedValue, req.Notes);

    private static ContactStatus? ParseStatus(string? status) =>
        Enum.TryParse<ContactStatus>(status, ignoreCase: true, out var s) ? s : null;

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}

public record UpsertContactRequest(
    int CompanyId,
    string? Name,
    string? Email,
    string? Phone,
    int? OwnerUserId,
    string? Status,
    string? Source,
    decimal? EstimatedValue,
    string? Notes
);

public record TagRequest(int CompanyId, string Tag);
