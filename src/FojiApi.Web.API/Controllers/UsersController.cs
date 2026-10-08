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

    /// <summary>Which guided tours / getting-started steps this person has finished.</summary>
    [HttpGet("me/onboarding")]
    public async Task<IActionResult> GetOnboarding()
        => Ok(await userService.GetOnboardingAsync(CurrentUser.UserId));

    [HttpPost("me/onboarding/{key}")]
    public async Task<IActionResult> CompleteOnboarding(string key)
        => Ok(await userService.CompleteOnboardingAsync(CurrentUser.UserId, key));

    /// <summary>"Show the tours again" from the profile menu.</summary>
    [HttpDelete("me/onboarding")]
    public async Task<IActionResult> ResetOnboarding()
        => Ok(await userService.ResetOnboardingAsync(CurrentUser.UserId));
}

public record UpdateUserRequest(
    [param: System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string? FirstName,

    [param: System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string? LastName
);

public record ChangePasswordRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    string CurrentPassword,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(128, MinimumLength = 8)]
    string NewPassword
);
