using FojiApi.Core.Exceptions;

namespace FojiApi.Core.Validation;

public static class PasswordValidator
{
    public static void Validate(string password)
    {
        var errors = new List<string>();
        if (password.Length < 8) errors.Add("at least 8 characters");
        if (!password.Any(char.IsUpper)) errors.Add("an uppercase letter");
        if (!password.Any(char.IsLower)) errors.Add("a lowercase letter");
        if (!password.Any(char.IsDigit)) errors.Add("a number");

        if (errors.Count > 0)
            throw new DomainException($"Password must contain {string.Join(", ", errors)}.");
    }
}
