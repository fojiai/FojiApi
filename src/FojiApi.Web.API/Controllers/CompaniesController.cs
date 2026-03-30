using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class CompaniesController(
    ICompanyService companyService,
    IAnalyticsService analyticsService,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet("check-slug")]
    public async Task<IActionResult> CheckSlug([FromQuery] string slug)
    {
        var available = await companyService.IsSlugAvailableAsync(slug);
        return Ok(new { available });
    }

    [HttpPost]
    public async Task<IActionResult> CreateCompany([FromBody] CreateCompanyRequest req)
    {
        var result = await companyService.CreateCompanyAsync(CurrentUser.UserId, req.Name, req.Slug, req.Description);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCompany(int id)
    {
        EnsureCompanyAccess(id, CompanyRole.User);
        return Ok(await companyService.GetCompanyAsync(id));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCompany(int id, [FromBody] UpdateCompanyRequest req)
    {
        EnsureCompanyAccess(id, CompanyRole.Admin);
        return Ok(await companyService.UpdateCompanyAsync(id, req.Name, req.Description));
    }

    [HttpGet("{id:int}/members")]
    public async Task<IActionResult> GetMembers(int id)
    {
        EnsureCompanyAccess(id, CompanyRole.User);
        return Ok(await companyService.GetMembersAsync(id));
    }

    [HttpDelete("{id:int}/members/{userId:int}")]
    public async Task<IActionResult> RemoveMember(int id, int userId)
    {
        EnsureCompanyAccess(id, CompanyRole.Admin);
        await companyService.RemoveMemberAsync(id, userId, CurrentUser.UserId);
        return NoContent();
    }

    [HttpPost("{id:int}/invite")]
    public async Task<IActionResult> InviteMember(int id, [FromBody] InviteMemberRequest req)
    {
        EnsureCompanyAccess(id, CompanyRole.Admin);
        await companyService.InviteMemberAsync(id, CurrentUser.UserId, req.Email, req.Role);
        return Ok(new { message = "Invitation sent." });
    }

    /// <summary>
    /// GET /api/companies/{id}/stats?from=2026-01-01&amp;to=2026-03-20
    /// Returns daily usage stats for charts. Defaults to last 30 days.
    /// </summary>
    [HttpGet("{id:int}/stats")]
    public async Task<IActionResult> GetStats(
        int id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        EnsureCompanyAccess(id, CompanyRole.User);
        var result = await analyticsService.GetCompanyStatsAsync(id, from, to);
        return Ok(result);
    }

    /// <summary>DELETE /api/companies/{id} — permanently deletes a company and all its data.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCompany(int id)
    {
        EnsureCompanyAccess(id, CompanyRole.Owner);
        await companyService.DeleteCompanyAsync(id, CurrentUser.UserId);
        return NoContent();
    }

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}

public record CreateCompanyRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string Name,

    [param: System.ComponentModel.DataAnnotations.StringLength(50)]
    [param: System.ComponentModel.DataAnnotations.RegularExpression(@"^[a-z0-9\-]+$", ErrorMessage = "Slug must be lowercase letters, numbers, and hyphens only.")]
    string? Slug,

    [param: System.ComponentModel.DataAnnotations.StringLength(500)]
    string? Description
);

public record UpdateCompanyRequest(
    [param: System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string? Name,

    [param: System.ComponentModel.DataAnnotations.StringLength(500)]
    string? Description
);

public record InviteMemberRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.EmailAddress]
    string Email,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.RegularExpression(@"^(admin|user)$", ErrorMessage = "Role must be 'admin' or 'user'.")]
    string Role
);
