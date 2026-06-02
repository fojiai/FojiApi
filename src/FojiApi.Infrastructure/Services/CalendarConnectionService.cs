using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FojiApi.Core.Entities;
using FojiApi.Core.Exceptions;
using FojiApi.Core.Interfaces.Services;
using FojiApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FojiApi.Infrastructure.Services;

public class CalendarConnectionService(
    FojiDbContext db,
    IEncryptionService encryption,
    IPlanEnforcementService planEnforcement,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory) : ICalendarConnectionService
{
    private static readonly string[] Scopes =
    [
        "https://www.googleapis.com/auth/calendar.events",
        "https://www.googleapis.com/auth/calendar.readonly",
        "https://www.googleapis.com/auth/userinfo.email",
        "openid"
    ];

    private string ClientId => configuration["GoogleCalendar:ClientId"]
        ?? throw new InvalidOperationException("GoogleCalendar:ClientId is not configured.");

    private string ClientSecret => configuration["GoogleCalendar:ClientSecret"]
        ?? throw new InvalidOperationException("GoogleCalendar:ClientSecret is not configured.");

    private string RedirectUri => configuration["GoogleCalendar:RedirectUri"]
        ?? throw new InvalidOperationException("GoogleCalendar:RedirectUri is not configured.");

    private string HmacSecret => configuration["GoogleCalendar:HmacSecret"]
        ?? throw new InvalidOperationException("GoogleCalendar:HmacSecret is not configured.");

    public string BuildAuthorizationUrl(int agentId, int companyId)
    {
        var state = BuildSignedState(agentId, companyId);
        var scope = Uri.EscapeDataString(string.Join(" ", Scopes));
        var redirectUri = Uri.EscapeDataString(RedirectUri);
        return $"https://accounts.google.com/o/oauth2/v2/auth" +
               $"?client_id={Uri.EscapeDataString(ClientId)}" +
               $"&redirect_uri={redirectUri}" +
               $"&response_type=code" +
               $"&scope={scope}" +
               $"&access_type=offline" +
               $"&prompt=consent" +
               $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<string> HandleCallbackAsync(string code, string state)
    {
        var (agentId, companyId) = VerifyState(state);

        // Plan gate — only proceed if the company's plan includes Google Calendar
        await planEnforcement.EnsureCanUseGoogleCalendarAsync(companyId);

        // Exchange code for tokens
        var client = httpClientFactory.CreateClient("GoogleOAuth");
        var tokenResponse = await ExchangeCodeAsync(client, code);

        if (string.IsNullOrEmpty(tokenResponse.RefreshToken))
            throw new DomainException(
                "Google did not return a refresh token. Please revoke access in your Google account settings and try again.");

        // Fetch connected account email
        var email = await GetGoogleAccountEmailAsync(client, tokenResponse.AccessToken);

        // Encrypt and upsert
        var encryptedToken = encryption.Encrypt(tokenResponse.RefreshToken);
        var existing = await db.AgentCalendarConnections
            .FirstOrDefaultAsync(c => c.AgentId == agentId);

        if (existing != null)
        {
            existing.GoogleAccountEmail = email;
            existing.EncryptedRefreshToken = encryptedToken;
            existing.IsActive = true;
            existing.ConnectedAt = DateTime.UtcNow;
        }
        else
        {
            db.AgentCalendarConnections.Add(new AgentCalendarConnection
            {
                AgentId = agentId,
                GoogleAccountEmail = email,
                EncryptedRefreshToken = encryptedToken,
                IsActive = true,
                ConnectedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        return email;
    }

    public async Task<CalendarConnectionStatus> GetStatusAsync(int agentId, int companyId)
    {
        var conn = await db.AgentCalendarConnections
            .Where(c => c.AgentId == agentId && c.IsActive)
            .Select(c => new { c.GoogleAccountEmail })
            .FirstOrDefaultAsync();

        return conn is null
            ? new CalendarConnectionStatus(false, null)
            : new CalendarConnectionStatus(true, conn.GoogleAccountEmail);
    }

    public async Task DisconnectAsync(int agentId, int companyId)
    {
        var conn = await db.AgentCalendarConnections
            .FirstOrDefaultAsync(c => c.AgentId == agentId && c.IsActive)
            ?? throw new NotFoundException("No active calendar connection found for this agent.");

        conn.IsActive = false;
        await db.SaveChangesAsync();
    }

    // ── State signing ─────────────────────────────────────────────────────────

    private string BuildSignedState(int agentId, int companyId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{agentId}:{companyId}:{timestamp}";
        var signature = ComputeHmac(payload);
        return $"{payload}:{signature}";
    }

    private (int agentId, int companyId) VerifyState(string state)
    {
        var parts = state.Split(':');
        if (parts.Length != 4)
            throw new DomainException("Invalid OAuth state parameter.");

        if (!int.TryParse(parts[0], out var agentId) || !int.TryParse(parts[1], out var companyId) ||
            !long.TryParse(parts[2], out var timestamp))
            throw new DomainException("Malformed OAuth state parameter.");

        var elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp;
        if (elapsed > 600)
            throw new DomainException("OAuth state has expired. Please try connecting again.");

        var payload = $"{agentId}:{companyId}:{timestamp}";
        var expectedSig = ComputeHmac(payload);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(parts[3]),
                Encoding.UTF8.GetBytes(expectedSig)))
            throw new DomainException("Invalid OAuth state signature.");

        return (agentId, companyId);
    }

    private string ComputeHmac(string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(HmacSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToBase64String(hash);
    }

    // ── Google API calls ──────────────────────────────────────────────────────

    private async Task<TokenResponse> ExchangeCodeAsync(HttpClient client, string code)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["redirect_uri"] = RedirectUri,
            ["grant_type"] = "authorization_code",
        });

        var response = await client.PostAsync("https://oauth2.googleapis.com/token", content);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new DomainException($"Failed to exchange OAuth code: {body}");

        return JsonSerializer.Deserialize<TokenResponse>(body, JsonSerializerOptions.Web)
            ?? throw new DomainException("Empty token response from Google.");
    }

    private async Task<string> GetGoogleAccountEmailAsync(HttpClient client, string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v1/userinfo");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(req);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new DomainException($"Failed to retrieve Google account info: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("email").GetString()
            ?? throw new DomainException("Google did not return an email address.");
    }

    private sealed record TokenResponse(
        string AccessToken,
        string? RefreshToken,
        int ExpiresIn,
        string TokenType);
}
