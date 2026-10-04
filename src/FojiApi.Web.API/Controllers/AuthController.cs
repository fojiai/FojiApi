using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FojiApi.Web.API.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("signup")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Signup([FromBody] SignupRequest req)
    {
        await authService.SignupAsync(req.Email, req.Password, req.FirstName, req.LastName);
        return Ok(new { message = "Account created. Please check your email to verify your account." });
    }

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        await authService.VerifyEmailAsync(token);
        return Ok(new { message = "Email verified successfully. You can now log in." });
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await authService.LoginAsync(req.Email, req.Password, req.Client, req.DeviceName);
        return Ok(result);
    }

    /// <summary>
    /// Mobile app: trade a refresh token for a fresh access token (and a rotated
    /// refresh token). 401 means the session is over — log in again.
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
        => Ok(await authService.RefreshAsync(req.RefreshToken));

    /// <summary>Mobile app logout: revokes the refresh token on the server.</summary>
    [HttpPost("logout")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        await authService.LogoutAsync(req.RefreshToken);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth-strict")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        await authService.ForgotPasswordAsync(req.Email);
        return Ok(new { message = "If an account exists with this email, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth-strict")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        await authService.ResetPasswordAsync(req.Token, req.NewPassword);
        return Ok(new { message = "Password reset successfully. You can now log in." });
    }
}

public record SignupRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.EmailAddress]
    [param: System.ComponentModel.DataAnnotations.StringLength(254)]
    string Email,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(128, MinimumLength = 8)]
    string Password,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string FirstName,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string LastName
);

public record LoginRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.EmailAddress]
    string Email,

    [param: System.ComponentModel.DataAnnotations.Required]
    string Password,

    /// <summary>"mobile" for the app — it also receives a refresh token.</summary>
    [param: System.ComponentModel.DataAnnotations.StringLength(20)]
    string? Client = null,

    [param: System.ComponentModel.DataAnnotations.StringLength(100)]
    string? DeviceName = null
);

public record RefreshRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(200)]
    string RefreshToken
);

public record ForgotPasswordRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.EmailAddress]
    string Email
);

public record ResetPasswordRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    string Token,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(128, MinimumLength = 8)]
    string NewPassword
);
