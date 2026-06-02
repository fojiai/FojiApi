using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace FojiApi.Web.API.Controllers;

/// <summary>
/// Handoff events — both internal (from foji-ai-api) and authenticated (dashboard) endpoints.
/// </summary>
public class HandoffsController(
    IHandoffService handoffService,
    ICurrentUserService currentUser,
    IConfiguration configuration) : BaseController(currentUser)
{
    // ── Internal endpoint (called by foji-ai-api) ─────────────────────────────

    [HttpPost("internal")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateHandoffInternal(
        [FromHeader(Name = "X-Internal-Key")] string? internalKey,
        [FromBody] CreateHandoffRequest req)
    {
        var expected = configuration["InternalApiKey"];
        if (string.IsNullOrEmpty(internalKey) || string.IsNullOrEmpty(expected)
            || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(internalKey),
                System.Text.Encoding.UTF8.GetBytes(expected)))
        {
            return Unauthorized();
        }

        var result = await handoffService.CreateHandoffAsync(
            req.AgentId, req.SessionId, req.UserMessage, req.Source ?? "widget");

        return CreatedAtAction(null, new { id = result.Id }, result);
    }

    // ── Authenticated dashboard endpoints ─────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetHandoffs([FromQuery] int companyId, [FromQuery] int? agentId = null)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        return Ok(await handoffService.GetHandoffsAsync(companyId, agentId));
    }

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}

public record CreateHandoffRequest(
    int AgentId,
    string SessionId,
    string? UserMessage,
    string? Source
);
