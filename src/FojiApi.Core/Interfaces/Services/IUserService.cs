namespace FojiApi.Core.Interfaces.Services;

public interface IUserService
{
    Task<UserProfileResult> GetProfileAsync(int userId);
    Task<UserProfileResult> UpdateProfileAsync(int userId, string? firstName, string? lastName);
    Task ChangePasswordAsync(int userId, string currentPassword, string newPassword);
}

public record UserProfileResult(
    int Id, string Email, string FirstName, string LastName,
    bool IsSuperAdmin, DateTime? EmailVerifiedAt,
    IEnumerable<UserCompanyResult> Companies
);
