using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlansController(IPlanService planService) : ControllerBase
{
    /// <summary>
    /// Returns active public plans. Super-admins may pass ?includeAll=true to get all plans
    /// including private and inactive ones (used by admin UI).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlans([FromQuery] bool includeAll = false)
    {
        if (includeAll)
        {
            // Only super-admins may request the full list
            var isSuperAdmin = User.FindFirst("isSuperAdmin")?.Value == "true";
            if (!isSuperAdmin)
                return Forbid();

            return Ok(await planService.GetAllPlansAsync());
        }

        return Ok(await planService.GetActivePlansAsync());
    }
}
