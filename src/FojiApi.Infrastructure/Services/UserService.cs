using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class UserService(FojiDbContext db) : IUserService
{
    public async Task<UserProfileResult> GetProfileAsync(int userId)
    {
        var user = await db.Users
            .Include(u => u.UserCompanies).ThenInclude(uc => uc.Company)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new NotFoundException("User not found.");

        return MapToResult(user);
    }

    public async Task<UserProfileResult> UpdateProfileAsync(int userId, string? firstName, string? lastName)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new NotFoundException("User not found.");

        if (firstName != null) user.FirstName = firstName.Trim();
        if (lastName != null) user.LastName = lastName.Trim();
        await db.SaveChangesAsync();

        return await GetProfileAsync(userId);
    }

    public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new NotFoundException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.HashedPassword))
            throw new DomainException("Current password is incorrect.");

        user.HashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await db.SaveChangesAsync();
    }

    private static UserProfileResult MapToResult(Core.Entities.User user) => new(
        user.Id, user.Email, user.FirstName, user.LastName,
        user.IsSuperAdmin, user.EmailVerifiedAt,
        user.UserCompanies.Where(uc => uc.IsActive).Select(uc => new UserCompanyResult(
            uc.CompanyId, uc.Company.Name, uc.Company.Slug, uc.Role.ToString().ToLower()))
    );
}
