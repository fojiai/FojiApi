using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Core.Entities;
using FojiApi.Core.Validation;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class AuthService(FojiDbContext db, IJwtService jwtService, IEmailService emailService) : IAuthService
{
    public async Task SignupAsync(string email, string password, string firstName, string lastName)
    {
        PasswordValidator.Validate(password);

        if (await db.Users.AnyAsync(u => u.Email == email.ToLower()))
            throw new ConflictException("An account with this email already exists.");

        var verificationToken = GenerateUrlSafeToken();

        db.Users.Add(new User
        {
            Email = email.ToLower().Trim(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(password),
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await db.SaveChangesAsync();

        await emailService.SendEmailVerificationAsync(email.ToLower(), firstName.Trim(), verificationToken);
    }

    public async Task VerifyEmailAsync(string token)
    {
        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.EmailVerificationToken == token &&
            u.EmailVerificationTokenExpiresAt > DateTime.UtcNow);

        if (user == null)
            throw new DomainException("Invalid or expired verification link.");

        user.EmailVerifiedAt = DateTime.UtcNow;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiresAt = null;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Mobile sessions last as long as the app is used at least once in this
    /// window: each refresh issues a new token with a fresh expiry.
    /// </summary>
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(60);

    public async Task<LoginResult> LoginAsync(
        string email, string password, string? client = null, string? deviceName = null)
    {
        var user = await db.Users
            .Include(u => u.UserCompanies).ThenInclude(uc => uc.Company)
            .FirstOrDefaultAsync(u => u.Email == email.ToLower());

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.HashedPassword))
            throw new DomainException("Invalid email or password.");

        if (user.EmailVerifiedAt == null)
            throw new DomainException("Please verify your email before logging in.");

        if (!user.IsActive)
            throw new DomainException("Your account has been deactivated.");

        var result = BuildLoginResult(user);

        // The website keeps its 24h cookie session; only the app gets a
        // refresh token, so phones don't log people out every day.
        if (string.Equals(client, "mobile", StringComparison.OrdinalIgnoreCase))
        {
            var refresh = IssueRefreshToken(user.Id, deviceName);
            await db.SaveChangesAsync();
            result = result with { RefreshToken = refresh };
        }

        return result;
    }

    public async Task<LoginResult> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new UnauthorizedAccessException("Session expired. Please log in again.");

        var hash = HashToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash)
            ?? throw new UnauthorizedAccessException("Session expired. Please log in again.");

        if (stored.RevokedAt != null)
        {
            // A rotated-away token came back: someone kept a copy. End every
            // session this user has rather than guess which one is genuine.
            if (stored.ReplacedByTokenHash != null)
                await RevokeAllForUserAsync(stored.UserId);
            throw new UnauthorizedAccessException("Session expired. Please log in again.");
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Session expired. Please log in again.");

        var user = await db.Users
            .Include(u => u.UserCompanies).ThenInclude(uc => uc.Company)
            .FirstOrDefaultAsync(u => u.Id == stored.UserId);

        // Deactivated or unverified since login: the refresh doesn't outlive that.
        if (user == null || !user.IsActive || user.EmailVerifiedAt == null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            throw new UnauthorizedAccessException("Session expired. Please log in again.");
        }

        // Rotate. The new access token also carries the user's current
        // companies, so joining or leaving one shows up without a re-login.
        var next = IssueRefreshToken(user.Id, stored.DeviceName);
        stored.RevokedAt = DateTime.UtcNow;
        stored.ReplacedByTokenHash = HashToken(next);
        await db.SaveChangesAsync();

        return BuildLoginResult(user) with { RefreshToken = next };
    }

    public async Task LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = HashToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored == null || stored.RevokedAt != null) return;
        stored.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private LoginResult BuildLoginResult(User user)
    {
        var activeCompanies = user.UserCompanies.Where(uc => uc.IsActive).ToList();
        return new LoginResult(
            Token: jwtService.GenerateToken(user, activeCompanies),
            UserId: user.Id,
            Email: user.Email,
            FirstName: user.FirstName,
            LastName: user.LastName,
            IsSuperAdmin: user.IsSuperAdmin,
            Companies: activeCompanies.Select(uc => new UserCompanyResult(
                uc.CompanyId, uc.Company.Name, uc.Company.Slug, uc.Role.ToString().ToLower()))
        );
    }

    /// <summary>Adds a new refresh token (caller saves) and returns the raw value.</summary>
    private string IssueRefreshToken(int userId, string? deviceName)
    {
        var raw = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48))
            .Replace("=", "").Replace("+", "-").Replace("/", "_");
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(raw),
            ExpiresAt = DateTime.UtcNow + RefreshTokenLifetime,
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim()[..Math.Min(100, deviceName.Trim().Length)],
        });
        return raw;
    }

    private async Task RevokeAllForUserAsync(int userId)
    {
        var now = DateTime.UtcNow;
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));
    }

    private static string HashToken(string raw) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    public async Task ForgotPasswordAsync(string email)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower());
        if (user == null) return; // Silent — don't leak user existence

        var resetToken = GenerateUrlSafeToken();
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        await db.SaveChangesAsync();

        await emailService.SendPasswordResetAsync(user.Email, user.FirstName, resetToken);
    }

    public async Task ResetPasswordAsync(string token, string newPassword)
    {
        PasswordValidator.Validate(newPassword);

        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == token &&
            u.PasswordResetTokenExpiresAt > DateTime.UtcNow);

        if (user == null)
            throw new DomainException("Invalid or expired reset link.");

        user.HashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        await db.SaveChangesAsync();

        // A password reset is often "someone else may have my account": log
        // every phone out.
        await RevokeAllForUserAsync(user.Id);
    }

    private static string GenerateUrlSafeToken()
        => Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("=", "").Replace("+", "-").Replace("/", "_");
}
