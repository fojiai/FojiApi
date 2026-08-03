using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using FojiApi.Core.Enums;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Http;

namespace FojiApi.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private readonly ClaimsPrincipal? _user = httpContextAccessor.HttpContext?.User;

    public int UserId =>
        int.TryParse(_user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : 0;

    public string Email =>
        _user?.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? string.Empty;

    public bool IsSuperAdmin =>
        _user?.FindFirst("isSuperAdmin")?.Value == "true";

    public bool IsAuthenticated =>
        _user?.Identity?.IsAuthenticated ?? false;

    public int? GetActiveCompanyId()
    {
        var roles = GetAllCompanyRoles().ToList();
        return roles.Count > 0 ? roles[0].CompanyId : null;
    }

    public CompanyRole? GetRoleInCompany(int companyId)
    {
        // NOTE: do not use FirstOrDefault here. GetAllCompanyRoles yields a value
        // tuple, so a no-match returns default((int, CompanyRole)) — and because
        // CompanyRole.Owner is 0, that silently reads back as "Owner", granting
        // every caller owner rights on every company. Return an explicit null.
        foreach (var (cid, role) in GetAllCompanyRoles())
        {
            if (cid == companyId) return role;
        }
        return null;
    }

    public bool HasRoleInCompany(int companyId, CompanyRole minimumRole)
    {
        var role = GetRoleInCompany(companyId);
        if (role == null) return false;
        // Owner > Admin > User
        return (int)role.Value <= (int)minimumRole;
    }

    public IEnumerable<(int CompanyId, CompanyRole Role)> GetAllCompanyRoles()
    {
        var companiesClaim = _user?.FindFirst("companies")?.Value;
        if (string.IsNullOrEmpty(companiesClaim)) yield break;

        var companies = JsonSerializer.Deserialize<List<CompanyRoleClaim>>(companiesClaim);
        if (companies == null) yield break;

        foreach (var c in companies)
        {
            if (Enum.TryParse<CompanyRole>(c.Role, true, out var role))
                yield return (c.CompanyId, role);
        }
    }

    private record CompanyRoleClaim(int CompanyId, string Role);
}
