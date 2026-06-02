using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

/// <summary>
/// Manages Google Calendar OAuth connections per agent.
/// OAuth callback is intentionally unauthenticated — Google redirects here after consent.
/// Security is enforced via HMAC-signed state parameter instead of JWT.
/// </summary>
public class CalendarConnectionController(
    ICalendarConnectionService calendarService,
    IAgentService agentService,
    ICurrentUserService currentUser,
    IConfiguration configuration) : BaseController(currentUser)
{
    /// <summary>Returns the current calendar connection status for the given agent.</summary>
    [HttpGet("/api/agents/{agentId:int}/calendar/status")]
    public async Task<IActionResult> GetStatus(int agentId)
    {
        var agent = await agentService.GetAgentAsync(agentId);
        EnsureCompanyAccess(agent.CompanyId, CompanyRole.User);
        var status = await calendarService.GetStatusAsync(agentId, agent.CompanyId);
        return Ok(status);
    }

    /// <summary>Returns the Google OAuth authorization URL. Plan-gated.</summary>
    [HttpGet("/api/agents/{agentId:int}/calendar/auth-url")]
    public async Task<IActionResult> GetAuthUrl(int agentId)
    {
        var agent = await agentService.GetAgentAsync(agentId);
        EnsureCompanyAccess(agent.CompanyId, CompanyRole.Admin);
        var authUrl = calendarService.BuildAuthorizationUrl(agentId, agent.CompanyId);
        return Ok(new { authUrl });
    }

    /// <summary>
    /// Google OAuth callback — no [Authorize]. Security enforced via HMAC-signed state.
    /// Exchanges the code for tokens, saves the connection, and redirects back to the agent settings page.
    /// </summary>
    [HttpGet("/api/calendar/oauth-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> OAuthCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
            return Redirect(BuildUiRedirect(null, errorMessage: "Google authorization was denied or failed."));

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect(BuildUiRedirect(null, errorMessage: "Missing OAuth parameters."));

        try
        {
            await calendarService.HandleCallbackAsync(code, state);

            // Extract agentId from state for the redirect (format: agentId:companyId:ts:sig)
            var agentId = int.Parse(state.Split(':')[0]);
            return Redirect(BuildUiRedirect(agentId));
        }
        catch (Exception ex)
        {
            var agentId = TryParseAgentId(state);
            return Redirect(BuildUiRedirect(agentId, errorMessage: ex.Message));
        }
    }

    /// <summary>Disconnects the calendar (soft-delete). Requires Admin role.</summary>
    [HttpDelete("/api/agents/{agentId:int}/calendar/disconnect")]
    public async Task<IActionResult> Disconnect(int agentId)
    {
        var agent = await agentService.GetAgentAsync(agentId);
        EnsureCompanyAccess(agent.CompanyId, CompanyRole.Admin);
        await calendarService.DisconnectAsync(agentId, agent.CompanyId);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string BuildUiRedirect(int? agentId, string? errorMessage = null)
    {
        var baseUrl = configuration["App:BaseUrl"] ?? "https://app.foji.ai";
        if (agentId is null)
            return $"{baseUrl}/agents?calendar_error={Uri.EscapeDataString(errorMessage ?? "Unknown error")}";

        var path = $"{baseUrl}/agents/{agentId}";
        return errorMessage is null
            ? $"{path}?calendar_connected=1"
            : $"{path}?calendar_error={Uri.EscapeDataString(errorMessage)}";
    }

    private static int? TryParseAgentId(string? state)
    {
        if (state is null) return null;
        var parts = state.Split(':');
        return parts.Length >= 1 && int.TryParse(parts[0], out var id) ? id : null;
    }

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}
