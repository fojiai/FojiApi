using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

/// <summary>The company's current plan. Read by the dashboard, the app and the plan gates.</summary>
public class SubscriptionsController(IBillingService billingService, ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet]
    public async Task<IActionResult> GetSubscription([FromQuery] int companyId)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, CompanyRole.User) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
        return Ok(await billingService.GetSubscriptionAsync(companyId));
    }
}
