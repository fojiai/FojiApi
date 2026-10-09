using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

/// <summary>
/// Paying for Foji through Asaas: billing profile (CPF/CNPJ), choosing or changing
/// a plan, cancel/resume, new card, invoices, and the Asaas webhook.
/// Everything except the webhook is for the company owner.
/// </summary>
public class BillingController(
    IBillingService billing,
    BillingWebhookService webhooks,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile([FromQuery] int companyId)
    {
        EnsureOwner(companyId);
        return Ok(await billing.GetProfileAsync(companyId));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] BillingProfileRequest req)
    {
        EnsureOwner(req.CompanyId);
        return Ok(await billing.UpdateProfileAsync(req.CompanyId,
            new UpdateBillingProfileRequest(req.Name, req.AccountType ?? "", req.CpfCnpj)));
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] ChoosePlanBody req)
    {
        EnsureOwner(req.CompanyId);
        return Ok(await billing.PreviewAsync(req.CompanyId, req.ToRequest()));
    }

    [HttpPost("choose-plan")]
    public async Task<IActionResult> ChoosePlan([FromBody] ChoosePlanBody req)
    {
        EnsureOwner(req.CompanyId);
        return Ok(await billing.ChoosePlanAsync(req.CompanyId, CurrentUser.UserId, req.ToRequest(), RemoteIp()));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CompanyBody req)
    {
        EnsureOwner(req.CompanyId);
        return Ok(await billing.CancelAsync(req.CompanyId));
    }

    [HttpPost("resume")]
    public async Task<IActionResult> Resume([FromBody] CompanyBody req)
    {
        EnsureOwner(req.CompanyId);
        return Ok(await billing.ResumeAsync(req.CompanyId, CurrentUser.UserId));
    }

    [HttpPost("cancel-pending-change")]
    public async Task<IActionResult> CancelPendingChange([FromBody] CompanyBody req)
    {
        EnsureOwner(req.CompanyId);
        return Ok(await billing.CancelPendingChangeAsync(req.CompanyId));
    }

    [HttpPost("update-card")]
    public async Task<IActionResult> UpdateCard([FromBody] CompanyBody req)
    {
        EnsureOwner(req.CompanyId);
        return Ok(await billing.UpdateCardAsync(req.CompanyId, CurrentUser.UserId));
    }

    [HttpGet("payments")]
    public async Task<IActionResult> Payments([FromQuery] int companyId)
    {
        EnsureOwner(companyId);
        return Ok(await billing.ListPaymentsAsync(companyId));
    }

    [HttpGet("checkouts/{id:int}")]
    public async Task<IActionResult> Checkout(int id, [FromQuery] int companyId)
    {
        EnsureOwner(companyId);
        return Ok(await billing.GetCheckoutAsync(companyId, id) ?? throw new NotFoundException("Checkout not found."));
    }

    /// <summary>
    /// Asaas webhook. Only HTTP 200 counts as delivered for Asaas (201/204 are failures),
    /// and the event is stored before answering; processing happens in the background.
    /// </summary>
    [HttpPost("webhook/asaas")]
    [AllowAnonymous]
    public async Task<IActionResult> AsaasWebhook(CancellationToken ct)
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync(ct);
        var token = Request.Headers["asaas-access-token"].FirstOrDefault();
        return await webhooks.IngestAsync(payload, token, ct) ? Ok() : Unauthorized();
    }

    private void EnsureOwner(int companyId)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, CompanyRole.Owner) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }

    /// <summary>The payer's IP (Asaas wants it on card charges). Behind the load balancer it's the first X-Forwarded-For hop.</summary>
    private string? RemoteIp() =>
        Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? HttpContext.Connection.RemoteIpAddress?.ToString();
}

public record CompanyBody(int CompanyId);

public record BillingProfileRequest(int CompanyId, string Name, string CpfCnpj, string? AccountType);

public record ChoosePlanBody(int CompanyId, int PlanId, string? Cycle, string? Method)
{
    public ChoosePlanRequest ToRequest() => new(PlanId, Cycle ?? "monthly", Method ?? "credit_card");
}
