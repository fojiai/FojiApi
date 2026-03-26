using FojiApi.Core.Enums;

namespace FojiApi.Core.Interfaces.Services;

public interface ICurrentUserService
{
    int UserId { get; }
    string Email { get; }
    bool IsSuperAdmin { get; }
    bool IsAuthenticated { get; }
    int? GetActiveCompanyId();
    CompanyRole? GetRoleInCompany(int companyId);
    bool HasRoleInCompany(int companyId, CompanyRole minimumRole);
    IEnumerable<(int CompanyId, CompanyRole Role)> GetAllCompanyRoles();
}
