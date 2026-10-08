using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

/// <summary>
/// Conversation history for owners and admins: every chat their agents had on
/// the website and WhatsApp, including what the team wrote. Members (the "user"
/// role) work from the inbox and don't get the full history.
/// </summary>
[Route("api/conversations")]
public class ConversationHistoryController(
    IConversationHistoryService history,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    /// <param name="channel">"site" or "whatsapp"; omit for both.</param>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int companyId, [FromQuery] int? agentId = null, [FromQuery] string? channel = null,
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        EnsureAdmin(companyId);
        if (channel is not (null or "" or "site" or "whatsapp"))
            throw new DomainException("Invalid channel. Use 'site' or 'whatsapp'.");
        return Ok(await history.ListAsync(companyId, agentId, channel, search, page, pageSize));
    }

    /// <param name="kind">"inbox" or "session", as returned by the list.</param>
    [HttpGet("{kind}/{id:int}")]
    public async Task<IActionResult> Get([FromRoute] string kind, [FromRoute] int id, [FromQuery] int companyId)
    {
        EnsureAdmin(companyId);
        var thread = await history.GetAsync(companyId, kind, id)
            ?? throw new NotFoundException("Conversation not found.");
        return Ok(thread);
    }

    private void EnsureAdmin(int companyId)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, CompanyRole.Admin) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException("Only owners and admins can see the conversation history.");
    }
}
