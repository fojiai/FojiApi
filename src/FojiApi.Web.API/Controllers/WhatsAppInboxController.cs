using System.Security.Cryptography;
using System.Text;
using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace FojiApi.Web.API.Controllers;

[Route("api/whatsapp/inbox")]
public class WhatsAppInboxController(
    IWhatsAppInboxService inbox,
    IConfiguration configuration,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations([FromQuery] int companyId, [FromQuery] int? agentId = null)
    {
        EnsureCompanyAccess(companyId);
        return Ok(await inbox.GetConversationsAsync(companyId, agentId));
    }

    [HttpGet("conversations/{id:int}")]
    public async Task<IActionResult> GetThread([FromRoute] int id, [FromQuery] int companyId)
    {
        EnsureCompanyAccess(companyId);
        var thread = await inbox.GetThreadAsync(companyId, id);
        return thread == null ? NotFound() : Ok(thread);
    }

    [HttpPost("conversations/{id:int}/reply")]
    public async Task<IActionResult> Reply([FromRoute] int id, [FromBody] ReplyRequest req)
    {
        EnsureCompanyAccess(req.CompanyId);
        return Ok(await inbox.SendReplyAsync(req.CompanyId, id, CurrentUser.UserId, req.Text));
    }

    [HttpPost("conversations/{id:int}/read")]
    public async Task<IActionResult> MarkRead([FromRoute] int id, [FromBody] MarkReadRequest req)
    {
        EnsureCompanyAccess(req.CompanyId);
        await inbox.MarkReadAsync(req.CompanyId, id);
        return NoContent();
    }

    [HttpPost("conversations/{id:int}/assign")]
    public async Task<IActionResult> Assign([FromRoute] int id, [FromBody] AssignRequest req)
    {
        EnsureCompanyAccess(req.CompanyId);
        var updated = await inbox.AssignAsync(req.CompanyId, id, req.UserId);
        return updated == null ? NotFound() : Ok(updated);
    }

    /// <summary>Called by foji-worker when a number is in Inbox mode. Not user-facing.</summary>
    [HttpPost("internal/inbound")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordInbound(
        [FromBody] RecordInboundRequest req,
        [FromHeader(Name = "X-Internal-Key")] string? internalKey)
    {
        var expected = configuration["InternalApiKey"];
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(internalKey)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(internalKey), Encoding.UTF8.GetBytes(expected)))
        {
            throw new ForbiddenException();
        }

        await inbox.RecordInboundAsync(
            req.AgentId, req.PhoneNumberId, req.WaId, req.ProfileName, req.WamId, req.Text,
            req.MessageType ?? "text", req.MediaS3Key, req.MediaContentType, req.MediaFileName);
        return NoContent();
    }

    private void EnsureCompanyAccess(int companyId)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, CompanyRole.User) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}

public record ReplyRequest(
    int CompanyId,
    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(3000, MinimumLength = 1)]
    string Text
);

public record MarkReadRequest(int CompanyId);

public record AssignRequest(int CompanyId, int? UserId);

public record RecordInboundRequest(
    int AgentId,
    string PhoneNumberId,
    string WaId,
    string? ProfileName,
    string? WamId,
    string Text,
    string? MessageType,
    string? MediaS3Key,
    string? MediaContentType,
    string? MediaFileName
);
