using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

[Route("api/crm/emails")]
public class CrmEmailsController(
    ICrmEmailService crmEmailService,
    IPlanEnforcementService planEnforcement,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet]
    public async Task<IActionResult> GetForContact([FromQuery] int companyId, [FromQuery] int contactId)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        return Ok(await crmEmailService.GetForContactAsync(companyId, contactId));
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendCrmEmailRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        var sent = await crmEmailService.SendAsync(req.CompanyId, CurrentUser.UserId,
            new SendCrmEmailInput(req.ContactId, req.DealId, req.ToEmail, req.Subject, req.Body));
        return Ok(sent);
    }

    [HttpPost("draft")]
    public async Task<IActionResult> Draft([FromBody] DraftCrmEmailRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.User);
        await planEnforcement.EnsureCanUseCrmAsync(req.CompanyId);
        var draft = await crmEmailService.DraftAsync(req.CompanyId, new DraftEmailRequest(req.ContactId, req.Goal, req.Tone));
        return Ok(draft);
    }

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}

public record SendCrmEmailRequest(
    int CompanyId,
    int ContactId,
    int? DealId,
    string ToEmail,
    string Subject,
    string Body
);

public record DraftCrmEmailRequest(
    int CompanyId,
    int? ContactId,
    string Goal,
    string? Tone
);
