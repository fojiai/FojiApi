using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class LeadsController(ILeadService leadService, ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet]
    public async Task<IActionResult> GetLeads([FromQuery] int companyId, [FromQuery] int? agentId = null)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        return Ok(await leadService.GetLeadsAsync(companyId, agentId));
    }

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}
