using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Services;

/// <summary>
/// Handles Meta / WhatsApp Cloud API webhooks.
/// Verifies HMAC-SHA256 signatures, parses the nested payload,
/// and forwards each inbound text message to the SQS queue for foji-worker.
/// </summary>
public class WhatsAppWebhookService(
    IAmazonSQS sqsClient,
    IConfiguration configuration,
    ILogger<WhatsAppWebhookService> logger
) : IWhatsAppWebhookService
{
    private readonly string _appSecret = configuration["Meta:AppSecret"]
        ?? throw new InvalidOperationException("Meta:AppSecret not configured");

    private readonly string _verifyToken = configuration["Meta:WebhookVerifyToken"]
        ?? throw new InvalidOperationException("Meta:WebhookVerifyToken not configured");

    private readonly string _sqsQueueUrl = configuration["AWS:SqsWhatsAppQueueUrl"]
        ?? throw new InvalidOperationException("AWS:SqsWhatsAppQueueUrl not configured");

    // ── Subscription verification (GET) ─────────────────────────────────────

    public string VerifySubscription(string? mode, string? verifyToken, string? challenge)
    {
        if (mode != "subscribe")
            throw new DomainException("hub.mode must be 'subscribe'.");

        if (verifyToken != _verifyToken)
            throw new ForbiddenException("hub.verify_token does not match.");

        if (string.IsNullOrEmpty(challenge))
            throw new DomainException("hub.challenge is required.");

        return challenge;
    }

    // ── Inbound message handling (POST) ──────────────────────────────────────

    public async Task HandleInboundAsync(byte[] rawBody, string? signature)
    {
        // 1. Verify HMAC-SHA256 signature
        VerifySignature(rawBody, signature);

        // 2. Parse payload
        var root = JsonDocument.Parse(rawBody).RootElement;

        if (!root.TryGetProperty("entry", out var entries))
        {
            logger.LogDebug("WhatsApp webhook: no 'entry' field — likely a status update, skipping.");
            return;
        }

        var sqsTasks = new List<Task>();

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes)) continue;

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value)) continue;

                // Phone number ID (identifies which agent to use)
                if (!value.TryGetProperty("metadata", out var metadata)) continue;
                var phoneNumberId = metadata.GetProperty("phone_number_id").GetString();

                if (!value.TryGetProperty("messages", out var messages)) continue;

                foreach (var msg in messages.EnumerateArray())
                {
                    // Only process text messages
                    if (!msg.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "text")
                    {
                        logger.LogDebug("Skipping non-text WhatsApp message (type={Type})",
                            typeEl.GetString());
                        continue;
                    }

                    var from = msg.GetProperty("from").GetString();
                    var messageId = msg.GetProperty("id").GetString();
                    var timestamp = msg.GetProperty("timestamp").GetString();
                    var text = msg.GetProperty("text").GetProperty("body").GetString();

                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var sqsPayload = JsonSerializer.Serialize(new
                    {
                        phone_number_id = phoneNumberId,
                        from,
                        message_id = messageId,
                        text,
                        timestamp
                    });

                    logger.LogInformation(
                        "Enqueuing WhatsApp message: from={From} phone_number_id={PhoneNumberId}",
                        from, phoneNumberId);

                    sqsTasks.Add(sqsClient.SendMessageAsync(new SendMessageRequest
                    {
                        QueueUrl = _sqsQueueUrl,
                        MessageBody = sqsPayload,
                        // Use phone number as deduplication group for FIFO queues
                        MessageGroupId = from,
                    }));
                }
            }
        }

        if (sqsTasks.Count > 0)
            await Task.WhenAll(sqsTasks);
    }

    // ── Signature verification ────────────────────────────────────────────────

    private void VerifySignature(byte[] rawBody, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
            throw new ForbiddenException("Missing X-Hub-Signature-256 header.");

        // Header format: "sha256=<hex_digest>"
        if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("X-Hub-Signature-256 must start with 'sha256='.");

        var provided = signature["sha256=".Length..];

        var keyBytes = Encoding.UTF8.GetBytes(_appSecret);
        var computed = Convert.ToHexString(
            HMACSHA256.HashData(keyBytes, rawBody)
        ).ToLowerInvariant();

        // Constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(provided.ToLowerInvariant())))
        {
            logger.LogWarning("WhatsApp webhook signature mismatch — possible spoofing attempt.");
            throw new ForbiddenException("Signature verification failed.");
        }
    }
}
