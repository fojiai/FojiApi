using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

[Route("api/crm/analytics")]
public class CrmAnalyticsController(
    ICrmAnalyticsService crmAnalytics,
    IPlanEnforcementService planEnforcement,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    /// <summary>CRM KPIs: pipeline, win rate, cycle length, sources, tasks.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] int companyId, [FromQuery] int days = 90)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(companyId);
        return Ok(await crmAnalytics.GetSummaryAsync(companyId, days));
    }

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}
