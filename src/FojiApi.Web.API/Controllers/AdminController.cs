using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

/// <summary>
/// System-admin-only endpoints. All actions require IsSuperAdmin = true.
/// </summary>
[Route("api/admin")]
public class AdminController(
    ISystemAdminInvitationService invitationService,
    IAdminCompanyService adminCompanyService,
    IPlanService planService,
    IPlatformSettingService platformSettingService,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    private void EnsureSuperAdmin()
    {
        if (!CurrentUser.IsSuperAdmin) throw new ForbiddenException();
    }

    // ── Admin invitations ────────────────────────────────────────────────────

    [HttpGet("invitations")]
    public async Task<IActionResult> ListInvitations()
    {
        EnsureSuperAdmin();
        return Ok(await invitationService.ListPendingAsync());
    }

    [HttpPost("invitations")]
    public async Task<IActionResult> InviteAdmin([FromBody] InviteAdminRequest req)
    {
        EnsureSuperAdmin();
        await invitationService.InviteAsync(CurrentUser.UserId, req.Email);
        return Ok(new { message = $"Invitation sent to {req.Email}." });
    }

    [HttpDelete("invitations/{id:int}")]
    public async Task<IActionResult> RevokeInvitation(int id)
    {
        EnsureSuperAdmin();
        await invitationService.RevokeAsync(id);
        return NoContent();
    }

    [HttpGet("invitations/preview")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> PreviewInvitation([FromQuery] string token)
        => Ok(await invitationService.GetInvitationPreviewAsync(token));

    [HttpPost("invitations/accept")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<IActionResult> AcceptInvitation([FromBody] AcceptAdminInviteRequest req)
    {
        await invitationService.AcceptAsync(req.Token, req.FirstName, req.LastName, req.Password);
        return Ok(new { message = "Admin account created. You can now log in." });
    }

    // ── Companies ─────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/admin/companies?search=&page=1&pageSize=20
    /// Paginated list of all companies (customers).
    /// </summary>
    [HttpGet("companies")]
    public async Task<IActionResult> ListCompanies(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        EnsureSuperAdmin();
        var (items, total) = await adminCompanyService.ListAsync(search, page, pageSize);
        return Ok(new { items, total, page, pageSize });
    }

    /// <summary>GET /api/admin/companies/{id}</summary>
    [HttpGet("companies/{id:int}")]
    public async Task<IActionResult> GetCompany(int id)
    {
        EnsureSuperAdmin();
        return Ok(await adminCompanyService.GetAsync(id));
    }

    /// <summary>
    /// POST /api/admin/companies
    /// Admin-creates a company (B2B or Pessoa Física) with an optional owner.
    /// If the owner email is new, a placeholder user is created and they should be sent an invite.
    /// </summary>
    [HttpPost("companies")]
    public async Task<IActionResult> CreateCompany([FromBody] CreateAdminCompanyRequest req)
    {
        EnsureSuperAdmin();
        var id = await adminCompanyService.CreateCompanyAsync(req, CurrentUser.UserId);
        return Ok(new { id, message = "Company created successfully." });
    }

    /// <summary>
    /// POST /api/admin/companies/{id}/assign-plan
    /// Assign any plan (including private custom plans) to a company,
    /// bypassing Stripe. Previous active subscriptions are cancelled.
    /// </summary>
    [HttpPost("companies/{id:int}/assign-plan")]
    public async Task<IActionResult> AssignPlan(int id, [FromBody] AssignPlanRequest req)
    {
        EnsureSuperAdmin();
        await adminCompanyService.AssignPlanAsync(id, req, CurrentUser.UserId);
        return Ok(new { message = "Plan assigned successfully." });
    }

    /// <summary>
    /// DELETE /api/admin/companies/{id}/subscription
    /// Remove the active subscription (revert to no plan).
    /// </summary>
    [HttpDelete("companies/{id:int}/subscription")]
    public async Task<IActionResult> RemovePlan(int id)
    {
        EnsureSuperAdmin();
        await adminCompanyService.RemovePlanAsync(id, CurrentUser.UserId);
        return NoContent();
    }

    /// <summary>PATCH /api/admin/companies/{id}/notes</summary>
    [HttpPatch("companies/{id:int}/notes")]
    public async Task<IActionResult> UpdateNotes(int id, [FromBody] UpdateNotesRequest req)
    {
        EnsureSuperAdmin();
        await adminCompanyService.UpdateNotesAsync(id, req.Notes);
        return Ok(new { message = "Notes updated." });
    }

    // ── Plans ─────────────────────────────────────────────────────────────────

    /// <summary>POST /api/admin/plans — Create a new plan (including private/custom ones).</summary>
    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromBody] UpsertPlanRequest req)
    {
        EnsureSuperAdmin();
        var result = await planService.CreatePlanAsync(req);
        return Ok(result);
    }

    /// <summary>PUT /api/admin/plans/{id} — Update a plan.</summary>
    [HttpPut("plans/{id:int}")]
    public async Task<IActionResult> UpdatePlan(int id, [FromBody] UpsertPlanRequest req)
    {
        EnsureSuperAdmin();
        var result = await planService.UpdatePlanAsync(id, req);
        return Ok(result);
    }

    /// <summary>DELETE /api/admin/plans/{id} — Soft-delete (deactivate) a plan.</summary>
    [HttpDelete("plans/{id:int}")]
    public async Task<IActionResult> DeletePlan(int id)
    {
        EnsureSuperAdmin();
        await planService.DeletePlanAsync(id);
        return NoContent();
    }

    // ── System stats ──────────────────────────────────────────────────────────

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        EnsureSuperAdmin();
        return Ok(await adminCompanyService.GetPlatformStatsAsync());
    }

    // ── Platform settings (API keys) ─────────────────────────────────────────

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        EnsureSuperAdmin();
        return Ok(await platformSettingService.GetAllAsync());
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpsertSetting([FromBody] UpsertSettingRequest req)
    {
        EnsureSuperAdmin();
        var result = await platformSettingService.UpsertAsync(req.Key, req.Value, req.Label, req.Category, req.IsSecret);
        return Ok(result);
    }

    [HttpDelete("settings/{key}")]
    public async Task<IActionResult> DeleteSetting(string key)
    {
        EnsureSuperAdmin();
        await platformSettingService.DeleteAsync(key);
        return NoContent();
    }
}

// ── Request records ───────────────────────────────────────────────────────────

public record InviteAdminRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.EmailAddress]
    string Email
);

public record AcceptAdminInviteRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    string Token,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string FirstName,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string LastName,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(128, MinimumLength = 8)]
    string Password
);

public record UpsertSettingRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(100)]
    string Key,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(2000)]
    string Value,

    [param: System.ComponentModel.DataAnnotations.StringLength(200)]
    string Label = "",

    [param: System.ComponentModel.DataAnnotations.StringLength(50)]
    string Category = "",

    bool IsSecret = true
);

public record UpdateNotesRequest(
    [param: System.ComponentModel.DataAnnotations.StringLength(2000)]
    string? Notes
);

