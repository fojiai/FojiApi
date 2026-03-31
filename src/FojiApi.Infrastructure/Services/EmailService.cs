using FojiApi.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Resend;

namespace FojiApi.Infrastructure.Services;

/// <summary>
/// Sends transactional emails via Resend (https://resend.com).
/// Configuration keys: Resend:ApiKey, Resend:FromEmail, Resend:FromName, App:BaseUrl
/// </summary>
public class EmailService(IResend resend, IConfiguration configuration) : IEmailService
{
    private readonly string _from = $"{configuration["Resend:FromName"] ?? "Foji AI"} <{configuration["Resend:FromEmail"] ?? "noreply@foji.ai"}>";
    private readonly string _appBaseUrl = configuration["App:BaseUrl"] ?? "https://app.foji.ai";

    public async Task SendEmailVerificationAsync(string toEmail, string firstName, string verificationToken)
    {
        var verifyUrl = $"{_appBaseUrl}/verify-email?token={verificationToken}";

        await SendAsync(toEmail, "Verifique seu e-mail — Foji AI", $"""
            <div style="font-family:sans-serif;max-width:520px;margin:0 auto;">
              <h2 style="color:#111;">Olá, {firstName}!</h2>
              <p>Obrigado por criar sua conta na <strong>Foji AI</strong>. Clique no botão abaixo para verificar seu e-mail:</p>
              <p style="margin:32px 0;">
                <a href="{verifyUrl}"
                   style="background:#FF2D2D;color:white;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:bold;display:inline-block;">
                  Verificar E-mail
                </a>
              </p>
              <p style="color:#666;font-size:13px;">Este link expira em 24 horas. Se você não criou esta conta, ignore este e-mail.</p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0;" />
              <p style="color:#999;font-size:12px;">Foji AI — Forje sua inteligência</p>
            </div>
            """);
    }

    public async Task SendPasswordResetAsync(string toEmail, string firstName, string resetToken)
    {
        var resetUrl = $"{_appBaseUrl}/reset-password?token={resetToken}";

        await SendAsync(toEmail, "Redefinição de senha — Foji AI", $"""
            <div style="font-family:sans-serif;max-width:520px;margin:0 auto;">
              <h2 style="color:#111;">Olá, {firstName}!</h2>
              <p>Recebemos uma solicitação para redefinir a senha da sua conta Foji AI.</p>
              <p style="margin:32px 0;">
                <a href="{resetUrl}"
                   style="background:#FF2D2D;color:white;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:bold;display:inline-block;">
                  Redefinir Senha
                </a>
              </p>
              <p style="color:#666;font-size:13px;">Este link expira em 1 hora. Se você não solicitou esta redefinição, ignore este e-mail.</p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0;" />
              <p style="color:#999;font-size:12px;">Foji AI — Forje sua inteligência</p>
            </div>
            """);
    }

    public async Task SendInvitationAsync(string toEmail, string companyName, string inviterName, string invitationToken, string role)
    {
        var acceptUrl = $"{_appBaseUrl}/accept-invitation?token={invitationToken}";

        await SendAsync(toEmail, $"Você foi convidado para {companyName} — Foji AI", $"""
            <div style="font-family:sans-serif;max-width:520px;margin:0 auto;">
              <h2 style="color:#111;">Você foi convidado!</h2>
              <p><strong>{inviterName}</strong> convidou você para fazer parte da equipe <strong>{companyName}</strong> na Foji AI como <strong>{role}</strong>.</p>
              <p style="margin:32px 0;">
                <a href="{acceptUrl}"
                   style="background:#FF2D2D;color:white;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:bold;display:inline-block;">
                  Aceitar Convite
                </a>
              </p>
              <p style="color:#666;font-size:13px;">Este convite expira em 7 dias.</p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0;" />
              <p style="color:#999;font-size:12px;">Foji AI — Forje sua inteligência</p>
            </div>
            """);
    }

    public async Task SendSystemAdminInvitationAsync(string toEmail, string inviterName, string token)
    {
        var acceptUrl = $"{_appBaseUrl}/accept-invitation?type=admin&token={token}";

        await SendAsync(toEmail, "Convite de Administrador do Sistema — Foji AI", $"""
            <div style="font-family:sans-serif;max-width:520px;margin:0 auto;">
              <h2 style="color:#111;">Você foi convidado como Administrador</h2>
              <p><strong>{inviterName}</strong> convidou você para ser um <strong>administrador do sistema</strong> na plataforma Foji AI.</p>
              <p>Como administrador, você terá acesso completo ao painel de controle da plataforma.</p>
              <p style="margin:32px 0;">
                <a href="{acceptUrl}"
                   style="background:#FF2D2D;color:white;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:bold;display:inline-block;">
                  Aceitar Convite de Admin
                </a>
              </p>
              <p style="color:#666;font-size:13px;">Este convite expira em 7 dias.</p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0;" />
              <p style="color:#999;font-size:12px;">Foji AI — Forje sua inteligência</p>
            </div>
            """);
    }

    public async Task SendTrialEndingAsync(string toEmail, string firstName, string companyName, int daysLeft)
    {
        var billingUrl = $"{_appBaseUrl}/billing";

        await SendAsync(toEmail, $"Seu trial termina em {daysLeft} dia{(daysLeft == 1 ? "" : "s")} — Foji AI", $"""
            <div style="font-family:sans-serif;max-width:520px;margin:0 auto;">
              <h2 style="color:#111;">Olá, {firstName}!</h2>
              <p>O período de trial de <strong>{companyName}</strong> na Foji AI termina em <strong>{daysLeft} dia{(daysLeft == 1 ? "" : "s")}</strong>.</p>
              <p>Para continuar usando a plataforma sem interrupção, assine um plano agora.</p>
              <p style="margin:32px 0;">
                <a href="{billingUrl}"
                   style="background:#FF2D2D;color:white;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:bold;display:inline-block;">
                  Ver Planos
                </a>
              </p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0;" />
              <p style="color:#999;font-size:12px;">Foji AI — Forje sua inteligência</p>
            </div>
            """);
    }

    public async Task SendPaymentFailedAsync(string toEmail, string firstName, string companyName)
    {
        var billingUrl = $"{_appBaseUrl}/billing";

        await SendAsync(toEmail, "Problema com seu pagamento — Foji AI", $"""
            <div style="font-family:sans-serif;max-width:520px;margin:0 auto;">
              <h2 style="color:#111;">Olá, {firstName}!</h2>
              <p>Houve um problema ao processar o pagamento da assinatura de <strong>{companyName}</strong>.</p>
              <p>Para evitar a interrupção do serviço, por favor atualize suas informações de pagamento.</p>
              <p style="margin:32px 0;">
                <a href="{billingUrl}"
                   style="background:#FF2D2D;color:white;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:bold;display:inline-block;">
                  Atualizar Pagamento
                </a>
              </p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0;" />
              <p style="color:#999;font-size:12px;">Foji AI — Forje sua inteligência</p>
            </div>
            """);
    }

    public async Task SendSubscriptionCancelledAsync(string toEmail, string firstName, string companyName)
    {
        var billingUrl = $"{_appBaseUrl}/billing";

        await SendAsync(toEmail, "Assinatura cancelada — Foji AI", $"""
            <div style="font-family:sans-serif;max-width:520px;margin:0 auto;">
              <h2 style="color:#111;">Olá, {firstName}!</h2>
              <p>A assinatura de <strong>{companyName}</strong> foi cancelada. Você ainda pode acessar a plataforma até o fim do período pago.</p>
              <p>Se foi um engano ou mudou de ideia, reative sua assinatura a qualquer momento.</p>
              <p style="margin:32px 0;">
                <a href="{billingUrl}"
                   style="background:#FF2D2D;color:white;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:bold;display:inline-block;">
                  Reativar Assinatura
                </a>
              </p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0;" />
              <p style="color:#999;font-size:12px;">Foji AI — Forje sua inteligência</p>
            </div>
            """);
    }

    public async Task SendContactFormAsync(string toEmail, string fromName, string fromEmail, string subject, string category, string message)
    {
        await SendAsync(toEmail, $"[Foji AI — {category}] {subject}", $"""
            <div style="font-family:sans-serif;max-width:520px;margin:0 auto;">
              <h2 style="color:#111;">Nova mensagem de contato</h2>
              <table style="width:100%;border-collapse:collapse;margin:16px 0;">
                <tr><td style="padding:8px 0;color:#666;width:100px;vertical-align:top;">De:</td><td style="padding:8px 0;"><strong>{fromName}</strong> &lt;{fromEmail}&gt;</td></tr>
                <tr><td style="padding:8px 0;color:#666;vertical-align:top;">Categoria:</td><td style="padding:8px 0;">{category}</td></tr>
                <tr><td style="padding:8px 0;color:#666;vertical-align:top;">Assunto:</td><td style="padding:8px 0;">{subject}</td></tr>
              </table>
              <div style="background:#f9f9f9;border:1px solid #eee;border-radius:8px;padding:16px;margin:16px 0;white-space:pre-wrap;">{message}</div>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0;" />
              <p style="color:#999;font-size:12px;">Foji AI — Forje sua inteligência</p>
            </div>
            """);
    }

    private async Task SendAsync(string toEmail, string subject, string html)
    {
        var message = new EmailMessage
        {
            From = _from,
            Subject = subject,
            HtmlBody = html,
        };
        message.To.Add(toEmail);

        await resend.EmailSendAsync(message);
    }
}
