namespace FojiApi.Core.Interfaces.Services;

public interface IAuthService
{
    Task SignupAsync(string email, string password, string firstName, string lastName);
    Task VerifyEmailAsync(string token);
    /// <param name="client">"mobile" also issues a refresh token (see RefreshToken).</param>
    Task<LoginResult> LoginAsync(string email, string password, string? client = null, string? deviceName = null);

    /// <summary>Trades a refresh token for a fresh access token and a rotated refresh token.</summary>
    Task<LoginResult> RefreshAsync(string refreshToken);

    /// <summary>Revokes a refresh token (the mobile app's logout). Unknown tokens are ignored.</summary>
    Task LogoutAsync(string refreshToken);
    Task ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(string token, string newPassword);
}

public record LoginResult(
    string Token,
    int UserId,
    string Email,
    string FirstName,
    string LastName,
    bool IsSuperAdmin,
    IEnumerable<UserCompanyResult> Companies,
    /// <summary>Only for mobile logins and refreshes; null for the website.</summary>
    string? RefreshToken = null
);

public record UserCompanyResult(int CompanyId, string CompanyName, string CompanySlug, string Role);
