using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class InvitationsController(IInvitationService invitationService, ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInvitation(string token)
        => Ok(await invitationService.GetInvitationAsync(token));

    [HttpPost("{token}/accept")]
    public async Task<IActionResult> AcceptInvitation(string token)
        => Ok(await invitationService.AcceptInvitationAsync(token, CurrentUser.UserId));
}
