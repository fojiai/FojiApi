using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class AgentsController(IAgentService agentService, ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet]
    public async Task<IActionResult> GetAgents([FromQuery] int companyId)
    {
        EnsureCompanyAccess(companyId, CompanyRole.User);
        return Ok(await agentService.GetAgentsAsync(companyId));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetAgent(int id)
    {
        var agent = await agentService.GetAgentAsync(id);
        EnsureCompanyAccess(agent.CompanyId, CompanyRole.User);
        return Ok(agent);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequest req)
    {
        EnsureCompanyAccess(req.CompanyId, CompanyRole.Admin);
        var result = await agentService.CreateAgentAsync(req.CompanyId, req.Name, req.Description, req.IndustryType, req.AgentLanguage, req.UserPrompt);
        return CreatedAtAction(nameof(GetAgent), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAgent(int id, [FromBody] UpdateAgentRequest req)
    {
        var existing = await agentService.GetAgentAsync(id);
        EnsureCompanyAccess(existing.CompanyId, CompanyRole.Admin);
        return Ok(await agentService.UpdateAgentAsync(id, req.Name, req.Description, req.UserPrompt, req.IsActive, req.AgentLanguage, req.WhatsAppEnabled, req.WhatsAppPhoneNumberId, req.SupportWhatsAppNumber, req.SalesWhatsAppNumber, req.SupportEmail, req.SalesEmail));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAgent(int id)
    {
        var existing = await agentService.GetAgentAsync(id);
        EnsureCompanyAccess(existing.CompanyId, CompanyRole.Admin);
        await agentService.DeleteAgentAsync(id);
        return NoContent();
    }

    [HttpPost("{id:int}/regenerate-token")]
    public async Task<IActionResult> RegenerateToken(int id)
    {
        var existing = await agentService.GetAgentAsync(id);
        EnsureCompanyAccess(existing.CompanyId, CompanyRole.Admin);
        var token = await agentService.RegenerateTokenAsync(id);
        return Ok(new { agentId = id, agentToken = token });
    }

    [HttpGet("{id:int}/embed-code")]
    public async Task<IActionResult> GetEmbedCode(int id, [FromQuery] string? widgetBaseUrl)
    {
        var existing = await agentService.GetAgentAsync(id);
        EnsureCompanyAccess(existing.CompanyId, CompanyRole.User);
        return Ok(await agentService.GetEmbedCodeAsync(id, widgetBaseUrl));
    }

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}

public record CreateAgentRequest(
    [property: System.ComponentModel.DataAnnotations.Required]
    int CompanyId,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string Name,

    [property: System.ComponentModel.DataAnnotations.StringLength(500)]
    string? Description,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.StringLength(50)]
    string IndustryType,

    [property: System.ComponentModel.DataAnnotations.StringLength(10)]
    string? AgentLanguage,

    [property: System.ComponentModel.DataAnnotations.StringLength(10000)]
    string? UserPrompt
);

public record UpdateAgentRequest(
    [property: System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    string? Name,

    [property: System.ComponentModel.DataAnnotations.StringLength(500)]
    string? Description,

    [property: System.ComponentModel.DataAnnotations.StringLength(10000)]
    string? UserPrompt,
    bool? IsActive,

    [property: System.ComponentModel.DataAnnotations.StringLength(10)]
    string? AgentLanguage,
    bool? WhatsAppEnabled,

    [property: System.ComponentModel.DataAnnotations.StringLength(50)]
    string? WhatsAppPhoneNumberId,

    [property: System.ComponentModel.DataAnnotations.StringLength(20)]
    string? SupportWhatsAppNumber,

    [property: System.ComponentModel.DataAnnotations.StringLength(20)]
    string? SalesWhatsAppNumber,

    [property: System.ComponentModel.DataAnnotations.EmailAddress]
    [property: System.ComponentModel.DataAnnotations.StringLength(254)]
    string? SupportEmail,

    [property: System.ComponentModel.DataAnnotations.EmailAddress]
    [property: System.ComponentModel.DataAnnotations.StringLength(254)]
    string? SalesEmail
);
