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
    /// <param name="status">"open" or "resolved"; omit for both.</param>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int companyId, [FromQuery] int? agentId = null, [FromQuery] string? status = null)
    {
        EnsureCompanyAccess(companyId);

        InboxConversationStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<InboxConversationStatus>(status, ignoreCase: true, out var parsed))
                throw new DomainException("Invalid status. Use 'open' or 'resolved'.");
            filter = parsed;
        }

        return Ok(await inbox.GetConversationsAsync(companyId, agentId, filter));
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

    [HttpPost("conversations/{id:int}/resolve")]
    public async Task<IActionResult> Resolve([FromRoute] int id, [FromBody] MarkReadRequest req)
    {
        EnsureCompanyAccess(req.CompanyId);
        var updated = await inbox.ResolveAsync(req.CompanyId, id);
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpPost("conversations/{id:int}/reopen")]
    public async Task<IActionResult> Reopen([FromRoute] int id, [FromBody] MarkReadRequest req)
    {
        EnsureCompanyAccess(req.CompanyId);
        var updated = await inbox.ReopenAsync(req.CompanyId, id);
        return updated == null ? NotFound() : Ok(updated);
    }

    /// <summary>Hybrid: a team member takes the conversation from the AI.</summary>
    [HttpPost("conversations/{id:int}/takeover")]
    public async Task<IActionResult> Takeover([FromRoute] int id, [FromBody] MarkReadRequest req)
    {
        EnsureCompanyAccess(req.CompanyId);
        var updated = await inbox.TakeoverAsync(req.CompanyId, id, CurrentUser.UserId);
        return updated == null ? NotFound() : Ok(updated);
    }

    /// <summary>Hybrid: hands the conversation back to the AI.</summary>
    [HttpPost("conversations/{id:int}/release")]
    public async Task<IActionResult> Release([FromRoute] int id, [FromBody] MarkReadRequest req)
    {
        EnsureCompanyAccess(req.CompanyId);
        var updated = await inbox.ReleaseToAiAsync(req.CompanyId, id);
        return updated == null ? NotFound() : Ok(updated);
    }

    /// <summary>
    /// Called by foji-worker when a number is in Inbox or Hybrid mode. Not
    /// user-facing. Returns whether the AI may answer: not a duplicate, and no
    /// person has taken the conversation over.
    /// </summary>
    [HttpPost("internal/inbound")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordInbound(
        [FromBody] RecordInboundRequest req,
        [FromHeader(Name = "X-Internal-Key")] string? internalKey)
    {
        EnsureInternalKey(internalKey);
        return Ok(await inbox.RecordInboundAsync(
            req.AgentId, req.PhoneNumberId, req.WaId, req.ProfileName, req.WamId, req.Text,
            req.MessageType ?? "text", req.MediaS3Key, req.MediaContentType, req.MediaFileName));
    }

    /// <summary>Hybrid: foji-worker records a reply the AI sent.</summary>
    [HttpPost("internal/ai-reply")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordAiReply(
        [FromBody] AiReplyRequest req,
        [FromHeader(Name = "X-Internal-Key")] string? internalKey)
    {
        EnsureInternalKey(internalKey);
        await inbox.RecordAiReplyAsync(req.AgentId, req.WaId, req.Text);
        return NoContent();
    }

    /// <summary>Hybrid: the AI decided a person is needed.</summary>
    [HttpPost("internal/escalate")]
    [AllowAnonymous]
    public async Task<IActionResult> Escalate(
        [FromBody] EscalateRequest req,
        [FromHeader(Name = "X-Internal-Key")] string? internalKey)
    {
        EnsureInternalKey(internalKey);
        await inbox.EscalateToHumanAsync(req.AgentId, req.WaId, req.CustomerMessage);
        return NoContent();
    }

    private void EnsureInternalKey(string? internalKey)
    {
        var expected = configuration["InternalApiKey"];
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(internalKey)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(internalKey), Encoding.UTF8.GetBytes(expected)))
        {
            throw new ForbiddenException();
        }
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

public record AiReplyRequest(
    int AgentId,
    string WaId,
    [param: System.ComponentModel.DataAnnotations.StringLength(8000, MinimumLength = 1)]
    string Text
);

public record EscalateRequest(
    int AgentId,
    string WaId,
    [param: System.ComponentModel.DataAnnotations.StringLength(4000)]
    string? CustomerMessage
);

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
