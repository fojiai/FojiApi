using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FojiApi.Web.API.Controllers;

/// <summary>
/// Handles inbound webhooks from external services (Meta/WhatsApp).
/// All endpoints are anonymous — authentication is via signature verification.
/// </summary>
[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
public class WebhooksController(IWhatsAppWebhookService whatsAppWebhookService) : ControllerBase
{
    // ── WhatsApp subscription verification (GET) ─────────────────────────────

    /// <summary>
    /// Meta calls this GET once when you set up the webhook in the developer portal.
    /// Must return hub.challenge exactly as sent.
    /// </summary>
    [HttpGet("whatsapp")]
    public IActionResult VerifyWhatsApp(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        try
        {
            var responseChallenge = whatsAppWebhookService.VerifySubscription(mode, verifyToken, challenge);
            return Content(responseChallenge, "text/plain");
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }

    // ── WhatsApp inbound messages (POST) ─────────────────────────────────────

    /// <summary>
    /// Meta sends all inbound messages and status updates here.
    /// We verify the HMAC-SHA256 signature then enqueue to SQS for foji-worker.
    /// Must always return 200 OK — Meta retries non-200 responses.
    /// </summary>
    [HttpPost("whatsapp")]
    public async Task<IActionResult> WhatsAppInbound()
    {
        // Read raw body for signature verification (must happen before body is read as model)
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        var rawBody = ms.ToArray();

        var signature = Request.Headers["X-Hub-Signature-256"].ToString();

        try
        {
            await whatsAppWebhookService.HandleInboundAsync(rawBody, signature);
        }
        catch (ForbiddenException)
        {
            // Return 403 so Meta knows something is wrong, but also log it
            return Forbid();
        }
        catch (Exception ex)
        {
            // Log but always return 200 to prevent Meta from retrying with the same bad payload
            HttpContext.RequestServices
                .GetRequiredService<ILogger<WebhooksController>>()
                .LogError(ex, "Unexpected error processing WhatsApp webhook");
        }

        // Meta requires a 200 response even if we couldn't process the message
        return Ok(new { status = "ok" });
    }
}
