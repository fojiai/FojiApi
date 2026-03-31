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
    Task SendPaymentFailedAsync(string toEmail, string firstName, string companyName);
    Task SendSubscriptionCancelledAsync(string toEmail, string firstName, string companyName);
}
