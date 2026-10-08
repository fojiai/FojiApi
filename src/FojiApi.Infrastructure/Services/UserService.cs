using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Core.Validation;
using FojiApi.Infrastructure.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    // Keys look like "tour:agent-new" or "step:embed". The cap keeps a buggy or
    // hostile client from turning one user row into a dumping ground.
    private static readonly Regex OnboardingKeyPattern = new(@"^[a-z]+:[a-z0-9-]{1,40}$", RegexOptions.Compiled);
    private const int MaxOnboardingKeys = 100;

    public async Task<OnboardingResult> GetOnboardingAsync(int userId)
    {
        var user = await db.Users.FindAsync(userId) ?? throw new NotFoundException("User not found.");
        return new OnboardingResult(ParseOnboarding(user.OnboardingProgress));
    }

    public async Task<OnboardingResult> CompleteOnboardingAsync(int userId, string key)
    {
        key = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (!OnboardingKeyPattern.IsMatch(key))
            throw new DomainException("Invalid onboarding key.");

        var user = await db.Users.FindAsync(userId) ?? throw new NotFoundException("User not found.");
        var done = ParseOnboarding(user.OnboardingProgress);
        if (!done.Contains(key) && done.Count < MaxOnboardingKeys)
        {
            done.Add(key);
            user.OnboardingProgress = JsonSerializer.Serialize(done);
            await db.SaveChangesAsync();
        }
        return new OnboardingResult(done);
    }

    public async Task<OnboardingResult> ResetOnboardingAsync(int userId)
    {
        var user = await db.Users.FindAsync(userId) ?? throw new NotFoundException("User not found.");
        user.OnboardingProgress = null;
        await db.SaveChangesAsync();
        return new OnboardingResult([]);
    }

    private static List<string> ParseOnboarding(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        PasswordValidator.Validate(newPassword);

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
