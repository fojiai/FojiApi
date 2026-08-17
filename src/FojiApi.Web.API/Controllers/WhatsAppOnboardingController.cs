using System.Security.Cryptography;
using System.Text;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Web.API.Controllers;

/// <summary>
/// One-click WhatsApp connection (Meta Embedded Signup).
/// The browser never sees the app secret or the resulting token.
/// </summary>
[Route("api/whatsapp/onboarding")]
public class WhatsAppOnboardingController(
    IWhatsAppOnboardingService onboarding,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    /// <summary>
    /// What the front-end needs to launch the Meta popup. Safe to expose: the
    /// app id and the Facebook Login configuration id are both public values.
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(onboarding.GetConfig());

    /// <summary>
    /// Finishes onboarding from the code Embedded Signup handed the browser.
    /// Admin or owner only — this attaches a paying channel to the company.
    /// </summary>
    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] CompleteOnboardingRequest req)
    {
        if (!CurrentUser.HasRoleInCompany(req.CompanyId, CompanyRole.Admin) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();

        var result = await onboarding.CompleteAsync(req.AgentId, req.Code, req.WabaId, req.PhoneNumberId);
        return Ok(result);
    }

    /// <summary>
    /// Called by foji-worker when Meta rejects our token for a tenant. Not
    /// user-facing — a dead connection has to surface somewhere, and the send
    /// path is where we usually find out first.
    /// </summary>
    [HttpPost("internal/needs-reconnect")]
    [AllowAnonymous]
    public async Task<IActionResult> FlagNeedsReconnect(
        [FromBody] NeedsReconnectRequest req,
        [FromHeader(Name = "X-Internal-Key")] string? internalKey,
        [FromServices] IConfiguration configuration,
        [FromServices] FojiDbContext db)
    {
        var expected = configuration["InternalApiKey"];
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(internalKey)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(internalKey), Encoding.UTF8.GetBytes(expected)))
        {
            throw new ForbiddenException();
        }

        var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == req.AgentId);
        if (agent == null) return NotFound();
        agent.WhatsAppNeedsReconnect = true;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record NeedsReconnectRequest(int AgentId);

public record CompleteOnboardingRequest(
    int CompanyId,
    int AgentId,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(2048, MinimumLength = 1)]
    string Code,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(64, MinimumLength = 1)]
    string WabaId,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(64, MinimumLength = 1)]
    string PhoneNumberId
);
