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
        // Defaults to the current contract; override when the Facebook Login
        // configuration was built from a template pinned to an older version.
        var sessionInfoVersion = configuration["Meta:EmbeddedSignupSessionInfoVersion"] ?? "3";
        return new WhatsAppOnboardingConfig(enabled, appId, configId, GraphVersion, sessionInfoVersion);
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
        var (token, expiresAt) = await ExchangeCodeAsync(http, appId, appSecret, code);

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
        agent.WhatsAppTokenExpiresAt = expiresAt;
        agent.WhatsAppNeedsReconnect = false;
        agent.WhatsAppEnabled = true;
        await db.SaveChangesAsync();

        var displayNumber = await GetDisplayNumberAsync(http, phoneNumberId, token);

        logger.LogInformation(
            "WhatsApp onboarded via Embedded Signup: agent={AgentId} waba={WabaId} phone_number_id={PhoneNumberId}",
            agentId, wabaId, phoneNumberId);

        return new WhatsAppOnboardingResult(phoneNumberId, displayNumber, wabaId);
    }

    private async Task<(string Token, DateTime? ExpiresAt)> ExchangeCodeAsync(
        HttpClient http, string appId, string appSecret, string code)
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
        return (tokenEl.GetString()!, ReadExpiry(doc.RootElement));
    }

    /// <summary>
    /// expires_in is seconds-from-now. It is absent for permanent tokens, and a
    /// null expiry is how we mark "never needs refreshing" downstream.
    /// </summary>
    private static DateTime? ReadExpiry(JsonElement root)
    {
        if (!root.TryGetProperty("expires_in", out var el)) return null;
        long seconds = el.ValueKind switch
        {
            JsonValueKind.Number => el.GetInt64(),
            JsonValueKind.String when long.TryParse(el.GetString(), out var parsed) => parsed,
            _ => 0,
        };
        return seconds > 0 ? DateTime.UtcNow.AddSeconds(seconds) : null;
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


    // ── Refresh ───────────────────────────────────────────────────────────────

    /// <summary>
    /// How early we refresh. Meta issues 60-day tokens, so refreshing at 15 days
    /// left leaves two weeks to notice and act on a failure — refreshing on the
    /// last day would turn one bad night into a dead channel.
    /// </summary>
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromDays(15);

    public async Task<bool> RefreshTokenAsync(int agentId, CancellationToken ct = default)
    {
        var agent = await db.Agents.FirstOrDefaultAsync(a => a.Id == agentId, ct)
            ?? throw new NotFoundException("Agent not found.");
        return await RefreshAgentAsync(agent, ct);
    }

    public async Task<(int Refreshed, int Failed)> RefreshExpiringAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow + RefreshWindow;

        // Only agents with a known expiry: a null expiry is a permanent token
        // (manually pasted System User token), which must never be touched.
        var due = await db.Agents
            .Where(a => a.WhatsAppTokenExpiresAt != null
                        && a.WhatsAppTokenExpiresAt < cutoff
                        && a.WhatsAppAccessTokenEncrypted != null
                        && !a.WhatsAppNeedsReconnect)
            .ToListAsync(ct);

        int refreshed = 0, failed = 0;
        foreach (var agent in due)
        {
            if (ct.IsCancellationRequested) break;
            if (await RefreshAgentAsync(agent, ct)) refreshed++; else failed++;
        }
        return (refreshed, failed);
    }

    private async Task<bool> RefreshAgentAsync(Core.Entities.Agent agent, CancellationToken ct)
    {
        var appId = configuration["Meta:AppId"];
        var appSecret = configuration["Meta:AppSecret"];
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
        {
            logger.LogWarning("Cannot refresh WhatsApp tokens: Meta:AppId/AppSecret not configured");
            return false;
        }
        if (string.IsNullOrEmpty(agent.WhatsAppAccessTokenEncrypted)) return false;

        string current;
        try
        {
            current = encryption.Decrypt(agent.WhatsAppAccessTokenEncrypted);
        }
        catch (Exception ex)
        {
            // A token we cannot read is a token we cannot refresh. Flag it rather
            // than retrying forever against a value we will never decrypt.
            logger.LogError(ex, "Could not decrypt the WhatsApp token for agent {AgentId}", agent.Id);
            await FlagForReconnectAsync(agent, ct);
            return false;
        }

        var url = $"{GraphBase}/oauth/access_token"
                  + "?grant_type=fb_exchange_token"
                  + $"&client_id={Uri.EscapeDataString(appId)}"
                  + $"&client_secret={Uri.EscapeDataString(appSecret)}"
                  + $"&fb_exchange_token={Uri.EscapeDataString(current)}"
                  + "&set_token_expires_in_60_days=true";

        var http = httpClientFactory.CreateClient();
        HttpResponseMessage resp;
        try
        {
            resp = await http.GetAsync(url, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Network trouble is not the customer's problem — leave the agent
            // alone and try again on the next pass. The old token still works.
            logger.LogWarning(ex, "WhatsApp token refresh for agent {AgentId} could not reach Meta", agent.Id);
            return false;
        }

        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogError(
                "WhatsApp token refresh rejected for agent {AgentId}: {Status} {Body}",
                agent.Id, resp.StatusCode, Truncate(body));
            await FlagForReconnectAsync(agent, ct);
            return false;
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("access_token", out var tokenEl))
        {
            logger.LogError("WhatsApp token refresh for agent {AgentId} returned no token", agent.Id);
            await FlagForReconnectAsync(agent, ct);
            return false;
        }

        agent.WhatsAppAccessTokenEncrypted = encryption.Encrypt(tokenEl.GetString()!);
        agent.WhatsAppTokenExpiresAt = ReadExpiry(doc.RootElement);
        agent.WhatsAppNeedsReconnect = false;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Refreshed the WhatsApp token for agent {AgentId}; now expires {ExpiresAt:u}",
            agent.Id, agent.WhatsAppTokenExpiresAt);
        return true;
    }

    private async Task FlagForReconnectAsync(Core.Entities.Agent agent, CancellationToken ct)
    {
        agent.WhatsAppNeedsReconnect = true;
        await db.SaveChangesAsync(ct);
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500];
}
