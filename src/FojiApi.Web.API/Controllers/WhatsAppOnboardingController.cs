using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

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
}

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
