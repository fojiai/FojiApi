using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace FojiApi.Web.API.Controllers;

public class MeetingsController(
    IMeetingService meetingService,
    ICurrentUserService currentUser,
    IConfiguration configuration) : BaseController(currentUser)
{
    // ── Internal endpoint (called by foji-ai-api after it creates the calendar event) ──

    [HttpPost("internal")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordMeetingInternal(
        [FromHeader(Name = "X-Internal-Key")] string? internalKey,
        [FromBody] RecordMeetingRequest req)
    {
        var expected = configuration["InternalApiKey"];
        if (string.IsNullOrEmpty(internalKey) || string.IsNullOrEmpty(expected)
            || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(internalKey),
                System.Text.Encoding.UTF8.GetBytes(expected)))
        {
            return Unauthorized();
        }

        var result = await meetingService.RecordMeetingAsync(new RecordMeetingInput(
            req.AgentId, req.GoogleEventId, req.MeetLink, req.HtmlLink, req.Title,
            req.StartsAt, req.EndsAt, req.AttendeeEmail, req.AttendeeName));

        return Ok(new { meetingId = result.MeetingId, contactId = result.ContactId });
    }

    // ── Authenticated dashboard endpoint ──

    [HttpGet]
    public async Task<IActionResult> GetMeetings([FromQuery] int companyId, [FromQuery] int? contactId = null)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        return Ok(await meetingService.GetMeetingsAsync(companyId, contactId));
    }

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}

public record RecordMeetingRequest(
    int AgentId,
    string GoogleEventId,
    string? MeetLink,
    string? HtmlLink,
    string Title,
    DateTime StartsAt,
    DateTime EndsAt,
    string? AttendeeEmail,
    string? AttendeeName
);
