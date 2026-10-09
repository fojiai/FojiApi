namespace FojiApi.Core.Interfaces.Services;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken);
    Task SendPasswordResetAsync(string toEmail, string firstName, string resetToken);
    Task SendInvitationAsync(string toEmail, string companyName, string inviterName, string invitationToken, string role);
    Task SendSystemAdminInvitationAsync(string toEmail, string inviterName, string token);

    // Contact form
    Task SendContactFormAsync(string toEmail, string fromName, string fromEmail, string subject, string category, string message);

    // Billing lifecycle emails
    Task SendTrialEndingAsync(string toEmail, string firstName, string companyName, int daysLeft);
    Task SendTrialEndedAsync(string toEmail, string firstName, string companyName);
    /// <summary>A charge is overdue, or the card was refused. invoiceUrl lets them pay right away.</summary>
    Task SendPaymentFailedAsync(string toEmail, string firstName, string companyName, string? invoiceUrl, bool cardRefused, DateTime? suspendsAt);
    Task SendAccessSuspendedAsync(string toEmail, string firstName, string companyName, string? invoiceUrl);
    /// <summary>accessUntil: when the paid period ends (null = access ended now).</summary>
    Task SendSubscriptionCancelledAsync(string toEmail, string firstName, string companyName, DateTime? accessUntil);
    /// <summary>A charge to pay through a link: Pix renewal, WhatsApp overage, upgrade difference.</summary>
    Task SendBillingInvoiceAsync(string toEmail, string firstName, string subject, string intro, decimal value, DateOnly dueDate, string invoiceUrl);

    // Human handoff notification
    Task SendHandoffNotificationAsync(string toEmail, string agentName, string companyName, string sessionId, string? userMessage);

    /// <summary>Sends a free-form CRM email (proposal / follow-up). Body is plain text; newlines become paragraphs.</summary>
    Task SendCrmEmailAsync(string toEmail, string subject, string body);
}
