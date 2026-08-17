using System.Security.Cryptography;
using System.Text;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Web.API.Controllers;

/// <summary>
/// Live WhatsApp message metering. Meta bills per message, so this is what
/// bounds our cost — not the conversation counter on the dashboard.
/// </summary>
[Route("api/whatsapp/usage")]
public class WhatsAppUsageController(
    IWhatsAppUsageService usage,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    /// <summary>Usage for the current billing period, for the dashboard.</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int companyId)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, CompanyRole.User) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();

        return Ok(await usage.GetUsageAsync(companyId));
    }

    /// <summary>
    /// Called by foji-worker immediately before it sends. This is both the meter
    /// and the gate: a denied response means the send must not happen.
    ///
    /// Takes an agent rather than a company so the worker does not have to know
    /// the tenancy mapping.
    /// </summary>
    [HttpPost("internal/consume")]
    [AllowAnonymous]
    public async Task<IActionResult> Consume(
        [FromBody] ConsumeUsageRequest req,
        [FromHeader(Name = "X-Internal-Key")] string? internalKey,
        [FromServices] IConfiguration configuration,
        [FromServices] FojiDbContext db)
    {
        var expected = configuration["InternalApiKey"];
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(internalKey)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(internalKey), Encoding.UTF8.GetBytes(expected)))
        {
            throw new ForbiddenException();
        }

        var companyId = await db.Agents
            .Where(a => a.Id == req.AgentId)
            .Select(a => (int?)a.CompanyId)
            .FirstOrDefaultAsync();
        if (companyId is null) return NotFound();

        var category = Enum.TryParse<WhatsAppMessageCategory>(req.Category, true, out var parsed)
            ? parsed
            : WhatsAppMessageCategory.Service;

        return Ok(await usage.TryConsumeAsync(companyId.Value, category));
    }
}

public record ConsumeUsageRequest(int AgentId, string? Category);
