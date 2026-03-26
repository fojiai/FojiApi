using FojiApi.Core.Entities;

namespace FojiApi.Core.Interfaces.Services;

public interface IJwtService
{
    string GenerateToken(User user, IEnumerable<UserCompany> userCompanies);
    string GenerateShortLivedToken(int userId, string purpose, TimeSpan duration);
    bool TryValidateShortLivedToken(string token, string expectedPurpose, out int userId);
}
