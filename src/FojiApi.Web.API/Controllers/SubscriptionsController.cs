using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class SubscriptionsController(IBillingService billingService, ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutRequest req)
    {
        EnsureOwner(req.CompanyId);
        var url = await billingService.CreateCheckoutSessionAsync(req.CompanyId, req.PlanId, CurrentUser.UserId);
        return Ok(new { checkoutUrl = url });
    }

    [HttpPost("portal")]
    public async Task<IActionResult> CustomerPortal([FromBody] PortalRequest req)
    {
        EnsureOwner(req.CompanyId);
        var url = await billingService.CreateCustomerPortalSessionAsync(req.CompanyId);
        return Ok(new { portalUrl = url });
    }

    [HttpGet]
    public async Task<IActionResult> GetSubscription([FromQuery] int companyId)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, CompanyRole.User) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
        return Ok(await billingService.GetSubscriptionAsync(companyId));
    }

    [HttpGet("verify-session")]
    public async Task<IActionResult> VerifyCheckoutSession([FromQuery] int companyId, [FromQuery] string sessionId)
    {
        EnsureOwner(companyId);
        return Ok(await billingService.VerifyCheckoutSessionAsync(companyId, sessionId));
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        var payload = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();
        await billingService.HandleWebhookAsync(payload, signature);
        return Ok();
    }

    private void EnsureOwner(int companyId)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, CompanyRole.Owner) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}

public record CreateCheckoutRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    int CompanyId,

    [param: System.ComponentModel.DataAnnotations.Required]
    int PlanId
);

public record PortalRequest(
    [param: System.ComponentModel.DataAnnotations.Required]
    int CompanyId
);
