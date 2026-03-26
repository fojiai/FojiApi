namespace FojiApi.Core.Interfaces.Services;

public interface IWhatsAppWebhookService
{
    /// <summary>
    /// Parses a raw Meta webhook payload, verifies the HMAC-SHA256 signature,
    /// and enqueues all valid inbound text messages to SQS for processing by foji-worker.
    /// </summary>
    /// <param name="rawBody">Raw request body bytes (used for signature verification).</param>
    /// <param name="signature">Value of the X-Hub-Signature-256 header (format: "sha256=&lt;hex&gt;").</param>
    Task HandleInboundAsync(byte[] rawBody, string? signature);

    /// <summary>
    /// Validates the Meta webhook subscription challenge.
    /// Returns the hub.challenge string if verify_token matches, otherwise throws.
    /// </summary>
    string VerifySubscription(string? mode, string? verifyToken, string? challenge);
}
