using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace FojiApi.Web.API.Controllers;

public class LeadsController(
    ILeadService leadService,
    IContactService contactService,
    ICurrentUserService currentUser,
    IConfiguration configuration) : BaseController(currentUser)
{
    // ── Internal endpoint (called by foji-ai-api) ─────────────────────────────
    // Writes the raw Lead AND find-or-creates the deduped Contact (single owner of the invariant).

    [HttpPost("internal")]
    [AllowAnonymous]
    public async Task<IActionResult> CaptureLeadInternal(
        [FromHeader(Name = "X-Internal-Key")] string? internalKey,
        [FromBody] CaptureLeadRequest req)
    {
        var expected = configuration["InternalApiKey"];
        if (string.IsNullOrEmpty(internalKey) || string.IsNullOrEmpty(expected)
            || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(internalKey),
                System.Text.Encoding.UTF8.GetBytes(expected)))
        {
            return Unauthorized();
        }

        var result = await contactService.CaptureLeadAndUpsertContactAsync(
            req.AgentId, req.SessionId, req.Name, req.Email, req.Phone, req.Source ?? "widget");

        return Ok(new { id = result.LeadId, contactId = result.ContactId, session_id = result.SessionId });
    }

    // ── Authenticated dashboard endpoints ─────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetLeads([FromQuery] int companyId, [FromQuery] int? agentId = null)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        return Ok(await leadService.GetLeadsAsync(companyId, agentId));
    }

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}

public record CaptureLeadRequest(
    int AgentId,
    string SessionId,
    string? Name,
    string? Email,
    string? Phone,
    string? Source
);
