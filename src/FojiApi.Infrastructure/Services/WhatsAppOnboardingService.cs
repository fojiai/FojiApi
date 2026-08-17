using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FojiApi.Infrastructure.Services;

/// <summary>
/// Embedded Signup onboarding. See IWhatsAppOnboardingService for the intent.
///
/// Everything that touches the app secret happens here, server-side. The browser
/// only ever holds a code that dies in 30 seconds and is useless on its own.
/// </summary>
public class WhatsAppOnboardingService(
    FojiDbContext db,
    IEncryptionService encryption,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<WhatsAppOnboardingService> logger) : IWhatsAppOnboardingService
{
    private string GraphVersion => configuration["Meta:GraphVersion"] ?? "v25.0";
    private string GraphBase => $"https://graph.facebook.com/{GraphVersion}";

    public WhatsAppOnboardingConfig GetConfig()
    {
        var appId = configuration["Meta:AppId"];
        var configId = configuration["Meta:EmbeddedSignupConfigId"];
        var enabled = !string.IsNullOrWhiteSpace(appId)
                      && !string.IsNullOrWhiteSpace(configId)
                      && !string.IsNullOrWhiteSpace(configuration["Meta:AppSecret"]);
        return new WhatsAppOnboardingConfig(enabled, appId, configId, GraphVersion);
    }

    public async Task<WhatsAppOnboardingResult> CompleteAsync(
        int agentId, string code, string wabaId, string phoneNumberId)
    {
        var appId = configuration["Meta:AppId"];
        var appSecret = configuration["Meta:AppSecret"];
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
            throw new DomainException(
                "WhatsApp onboarding is not configured on this server. An administrator must set Meta:AppId and Meta:AppSecret.");

        var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == agentId)
            ?? throw new NotFoundException("Agent not found.");

        var http = httpClientFactory.CreateClient();

        // ── 1. Code → business token ──────────────────────────────────────────
        // No redirect_uri: the Embedded Signup code is exchanged directly. The
        // resulting token is scoped to this one customer's WABA and does not
        // expire, which is what makes the manual System User dance unnecessary.
        var token = await ExchangeCodeAsync(http, appId, appSecret, code);

        // ── 2. Point their WABA's webhooks at us ──────────────────────────────
        // Without this the customer is connected but silent: Meta has nowhere to
        // deliver their messages. This is the step people miss doing it by hand.
        await SubscribeAppAsync(http, wabaId, token);

        // ── 3. Register the number on Cloud API ───────────────────────────────
        // Needs a 6-digit two-step PIN. We generate it, store it encrypted, and
        // never show it — the customer has no reason to know it exists, but
        // re-registering later requires the same one.
        var pin = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
        await RegisterPhoneNumberAsync(http, phoneNumberId, pin, token);

        // ── 4. Persist ────────────────────────────────────────────────────────
        agent.WhatsAppPhoneNumberId = phoneNumberId;
        agent.WhatsAppBusinessAccountId = wabaId;
        agent.WhatsAppAccessTokenEncrypted = encryption.Encrypt(token);
        agent.WhatsAppPinEncrypted = encryption.Encrypt(pin);
        agent.WhatsAppEnabled = true;
        await db.SaveChangesAsync();

        var displayNumber = await GetDisplayNumberAsync(http, phoneNumberId, token);

        logger.LogInformation(
            "WhatsApp onboarded via Embedded Signup: agent={AgentId} waba={WabaId} phone_number_id={PhoneNumberId}",
            agentId, wabaId, phoneNumberId);

        return new WhatsAppOnboardingResult(phoneNumberId, displayNumber, wabaId);
    }

    private async Task<string> ExchangeCodeAsync(HttpClient http, string appId, string appSecret, string code)
    {
        var url = $"{GraphBase}/oauth/access_token"
                  + $"?client_id={Uri.EscapeDataString(appId)}"
                  + $"&client_secret={Uri.EscapeDataString(appSecret)}"
                  + $"&code={Uri.EscapeDataString(code)}";

        var resp = await http.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            // Never log the body verbatim — it can echo the code back.
            logger.LogError("Embedded Signup token exchange failed: {Status}", resp.StatusCode);
            throw new DomainException(MetaError(body)
                ?? "Could not complete the WhatsApp connection. Please try again.");
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("access_token", out var tokenEl))
            throw new DomainException("Meta did not return an access token. Please try again.");
        return tokenEl.GetString()!;
    }

    private async Task SubscribeAppAsync(HttpClient http, string wabaId, string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{GraphBase}/{wabaId}/subscribed_apps");
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            logger.LogError("Subscribing app to WABA {WabaId} failed: {Status} {Body}", wabaId, resp.StatusCode, Truncate(body));
            throw new DomainException(MetaError(body)
                ?? "Connected, but we could not subscribe to this WhatsApp account's messages. Please try again.");
        }
    }

    private async Task RegisterPhoneNumberAsync(HttpClient http, string phoneNumberId, string pin, string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{GraphBase}/{phoneNumberId}/register")
        {
            Content = JsonContent.Create(new { messaging_product = "whatsapp", pin }),
        };
        req.Headers.Authorization = new("Bearer", token);
        var resp = await http.SendAsync(req);
        if (resp.IsSuccessStatusCode) return;

        var body = await resp.Content.ReadAsStringAsync();

        // A number that is already registered is a success for our purposes —
        // Coexistence onboarding hands us numbers that Meta already registered.
        if (body.Contains("already registered", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Phone {PhoneNumberId} was already registered — continuing", phoneNumberId);
            return;
        }

        logger.LogError("Registering phone {PhoneNumberId} failed: {Status} {Body}", phoneNumberId, resp.StatusCode, Truncate(body));
        throw new DomainException(MetaError(body)
            ?? "Connected, but we could not finish registering this number. Please try again.");
    }

    /// <summary>Best-effort pretty number for the UI. Never fails the onboarding.</summary>
    private async Task<string?> GetDisplayNumberAsync(HttpClient http, string phoneNumberId, string token)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{GraphBase}/{phoneNumberId}?fields=display_phone_number");
            req.Headers.Authorization = new("Bearer", token);
            var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.TryGetProperty("display_phone_number", out var el) ? el.GetString() : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read display_phone_number for {PhoneNumberId}", phoneNumberId);
            return null;
        }
    }

    /// <summary>Meta's own message, which is usually more useful than ours.</summary>
    private static string? MetaError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                var msg = err.TryGetProperty("error_user_msg", out var userMsg) ? userMsg.GetString() : null;
                msg ??= err.TryGetProperty("message", out var m) ? m.GetString() : null;
                if (!string.IsNullOrWhiteSpace(msg)) return $"WhatsApp: {msg}";
            }
        }
        catch (JsonException) { /* not JSON — fall through to our own wording */ }
        return null;
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500];
}
