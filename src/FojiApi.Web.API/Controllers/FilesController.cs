using FojiApi.Core.Enums;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class FilesController(
    IFileService fileService,
    IAgentService agentService,
    ICurrentUserService currentUser) : BaseController(currentUser)
{
    [HttpGet]
    public async Task<IActionResult> GetFilesByAgent([FromQuery] int agentId)
    {
        var agent = await agentService.GetAgentAsync(agentId);
        EnsureCompanyAccess(agent.CompanyId, CompanyRole.User);
        return Ok(await fileService.GetFilesByAgentAsync(agentId));
    }

    [HttpPost("upload")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromQuery] int agentId, IFormFile file)
    {
        var agent = await agentService.GetAgentAsync(agentId);
        EnsureCompanyAccess(agent.CompanyId, CompanyRole.Admin);

        await using var stream = file.OpenReadStream();
        var result = await fileService.UploadAsync(agentId, stream, file.FileName, file.Length, file.ContentType);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetFile(int id)
    {
        var file = await fileService.GetFileAsync(id);
        var agent = await agentService.GetAgentAsync(file.AgentId);
        EnsureCompanyAccess(agent.CompanyId, CompanyRole.User);
        return Ok(file);
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var file = await fileService.GetFileAsync(id);
        var agent = await agentService.GetAgentAsync(file.AgentId);
        EnsureCompanyAccess(agent.CompanyId, CompanyRole.User);
        var url = await fileService.GetDownloadUrlAsync(id);
        return Ok(new { downloadUrl = url, expiresInMinutes = 15 });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteFile(int id)
    {
        var file = await fileService.GetFileAsync(id);
        var agent = await agentService.GetAgentAsync(file.AgentId);
        EnsureCompanyAccess(agent.CompanyId, CompanyRole.Admin);
        await fileService.DeleteFileAsync(id);
        return NoContent();
    }

    private void EnsureCompanyAccess(int companyId, CompanyRole minimum)
    {
        if (!CurrentUser.HasRoleInCompany(companyId, minimum) && !CurrentUser.IsSuperAdmin)
            throw new ForbiddenException();
    }
}
