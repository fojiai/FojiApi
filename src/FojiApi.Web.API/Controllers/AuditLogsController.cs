using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class AuditLogsController(IAuditLogService auditLogService, ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int? companyId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (companyId.HasValue && !CurrentUser.HasRoleInCompany(companyId.Value, CompanyRole.Admin) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();

        if (!companyId.HasValue && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();

        return Ok(await auditLogService.GetLogsAsync(companyId, page, pageSize));
    }
}
