namespace FojiApi.Core.Interfaces.Services;

public interface IAuthService
{
    Task SignupAsync(string email, string password, string firstName, string lastName);
    Task VerifyEmailAsync(string token);
    Task<LoginResult> LoginAsync(string email, string password);
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
    IEnumerable<UserCompanyResult> Companies
);

public record UserCompanyResult(int CompanyId, string CompanyName, string CompanySlug, string Role);
