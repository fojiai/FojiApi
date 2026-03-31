using FojiApi.Core.Entities;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FojiApi.Web.API.Controllers;

public class ContactController(
    IEmailService emailService,
    IPlatformSettingService platformSettingService,
    ICurrentUserService currentUser,
    IConfiguration configuration,
    FojiDbContext db) : BaseController(currentUser)
{
    [HttpPost]
    public async Task<IActionResult> SendContactForm([FromBody] ContactFormRequest req)
    {
        var senderName = req.Name?.Trim() ?? CurrentUser.Email;

        // Save to database
        var submission = new ContactSubmission
        {
            UserId = CurrentUser.UserId,
            Name = senderName,
            Email = CurrentUser.Email,
            Category = req.Category.Trim(),
            Subject = req.Subject.Trim(),
            Message = req.Message.Trim(),
        };
        db.ContactSubmissions.Add(submission);
        await db.SaveChangesAsync();

        // Send email notification
        var contactEmail = await platformSettingService.GetValueAsync("CONTACT_EMAIL");
        if (string.IsNullOrEmpty(contactEmail))
            contactEmail = configuration["Resend:FromEmail"] ?? "support@foji.ai";

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
