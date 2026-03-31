using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

public class ContactController(
    IEmailService emailService,
    IPlatformSettingService platformSettingService,
    ICurrentUserService currentUser,
    IConfiguration configuration) : BaseController(currentUser)
{
    [HttpPost]
    public async Task<IActionResult> SendContactForm([FromBody] ContactFormRequest req)
    {
        // Get recipient email from PlatformSettings, fallback to Resend:FromEmail
        var contactEmail = await platformSettingService.GetValueAsync("CONTACT_EMAIL");
        if (string.IsNullOrEmpty(contactEmail))
            contactEmail = configuration["Resend:FromEmail"] ?? "support@foji.ai";

        var senderName = req.Name?.Trim() ?? CurrentUser.Email;

        await emailService.SendContactFormAsync(
            toEmail: contactEmail,
            fromName: senderName,
            fromEmail: CurrentUser.Email,
            subject: req.Subject.Trim(),
            category: req.Category.Trim(),
            message: req.Message.Trim()
        );

        return Ok(new { message = "Message sent successfully." });
    }
}

public record ContactFormRequest(
    [param: System.ComponentModel.DataAnnotations.StringLength(100)]
    string? Name,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(50)]
    string Category,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(200)]
    string Subject,

    [param: System.ComponentModel.DataAnnotations.Required]
    [param: System.ComponentModel.DataAnnotations.StringLength(5000)]
    string Message
);
