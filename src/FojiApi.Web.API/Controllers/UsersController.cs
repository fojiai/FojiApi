using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class UsersController(IUserService userService, ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
        => Ok(await userService.GetProfileAsync(CurrentUser.UserId));

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateUserRequest req)
        => Ok(await userService.UpdateProfileAsync(CurrentUser.UserId, req.FirstName, req.LastName));

    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        await userService.ChangePasswordAsync(CurrentUser.UserId, req.CurrentPassword, req.NewPassword);
        return Ok(new { message = "Password changed successfully." });
    }
}

public record UpdateUserRequest(
    [property: System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string? FirstName,

    [property: System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string? LastName
);

public record ChangePasswordRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    string CurrentPassword,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(128, MinimumLength = 8)]
    string NewPassword
);
