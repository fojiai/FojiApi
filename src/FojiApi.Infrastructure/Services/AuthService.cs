using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Core.Entities;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Infrastructure.Services;

public class AuthService(FojiDbContext db, IJwtService jwtService, IEmailService emailService) : IAuthService
{
    public async Task SignupAsync(string email, string password, string firstName, string lastName)
    {
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

    public async Task<LoginResult> LoginAsync(string email, string password)
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

        var activeCompanies = user.UserCompanies.Where(uc => uc.IsActive).ToList();
        var token = jwtService.GenerateToken(user, activeCompanies);

        return new LoginResult(
            Token: token,
            UserId: user.Id,
            Email: user.Email,
            FirstName: user.FirstName,
            LastName: user.LastName,
            IsSuperAdmin: user.IsSuperAdmin,
            Companies: activeCompanies.Select(uc => new UserCompanyResult(
                uc.CompanyId, uc.Company.Name, uc.Company.Slug, uc.Role.ToString().ToLower()))
        );
    }

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
        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == token &&
            u.PasswordResetTokenExpiresAt > DateTime.UtcNow);

        if (user == null)
            throw new DomainException("Invalid or expired reset link.");

        user.HashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        await db.SaveChangesAsync();
    }

    private static string GenerateUrlSafeToken()
        => Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("=", "").Replace("+", "-").Replace("/", "_");
}
