using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FojiApi.Web.API.Controllers;

public class InvitationsController(IInvitationService invitationService, ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInvitation(string token)
        => Ok(await invitationService.GetInvitationAsync(token));

    /// <summary>For people who already have an account: log in first, then accept.</summary>
    [HttpPost("{token}/accept")]
    public async Task<IActionResult> AcceptInvitation(string token)
        => Ok(await invitationService.AcceptInvitationAsync(token, CurrentUser.UserId));

    /// <summary>
    /// For people without an account: creates it (already verified — the invite
    /// link proves the email) and joins the company. Returns a login result.
    /// </summary>
    [HttpPost("{token}/register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterAndAccept(string token, [FromBody] RegisterFromInvitationRequest req)
        => Ok(await invitationService.RegisterAndAcceptAsync(token, req.FirstName, req.LastName, req.Password));
}

public record RegisterFromInvitationRequest(
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
