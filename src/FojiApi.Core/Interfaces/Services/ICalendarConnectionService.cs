namespace FojiApi.Core.Interfaces.Services;

public interface ICalendarConnectionService
{
    /// <summary>Builds the Google OAuth2 authorization URL with HMAC-signed state.</summary>
    string BuildAuthorizationUrl(int agentId, int companyId);

    /// <summary>Exchanges the OAuth code for tokens, saves the connection, and returns the connected email.</summary>
    Task<string> HandleCallbackAsync(string code, string state);

    /// <summary>Returns the calendar connection status for the given agent.</summary>
    Task<CalendarConnectionStatus> GetStatusAsync(int agentId, int companyId);

    /// <summary>Soft-deletes the calendar connection (IsActive = false).</summary>
    Task DisconnectAsync(int agentId, int companyId);
}

public record CalendarConnectionStatus(bool IsConnected, string? GoogleAccountEmail);
