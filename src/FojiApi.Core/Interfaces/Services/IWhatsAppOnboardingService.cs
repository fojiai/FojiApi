namespace FojiApi.Core.Interfaces.Services;

/// <summary>
/// One-click WhatsApp connection via Meta's Embedded Signup.
///
/// The customer clicks a button, picks their number inside Meta's own popup, and
/// everything after that happens here: we exchange the short-lived code for a
/// business token, point their WhatsApp Business Account's webhooks at us, and
/// register the number on Cloud API. The customer never sees a token, a
/// phone_number_id, or the Meta developer panel.
/// </summary>
public interface IWhatsAppOnboardingService
{
    /// <summary>Whether Embedded Signup is configured on this deployment.</summary>
    WhatsAppOnboardingConfig GetConfig();

    /// <summary>
    /// Completes onboarding for an agent from the code Embedded Signup returned.
    /// The code is single-use and expires in about 30 seconds.
    /// </summary>
    Task<WhatsAppOnboardingResult> CompleteAsync(
        int agentId, string code, string wabaId, string phoneNumberId);

    /// <summary>
    /// Refreshes one agent's Meta token. Returns false when Meta refuses, which
    /// means the customer has to reconnect — the agent is flagged for that.
    /// Safe to call early: the old token keeps working until its own expiry, so
    /// there is never a window where the connection is down.
    /// </summary>
    Task<bool> RefreshTokenAsync(int agentId, CancellationToken ct = default);

    /// <summary>
    /// Refreshes every connection close to expiry. Returns how many were
    /// refreshed and how many now need the customer to reconnect.
    /// </summary>
    Task<(int Refreshed, int Failed)> RefreshExpiringAsync(CancellationToken ct = default);
}

/// <param name="Enabled">False when AppId/ConfigId are unset — the UI then offers manual setup.</param>
/// <param name="SessionInfoVersion">
/// Which Embedded Signup session-info contract the popup should speak. Server-driven
/// because it depends on which Facebook Login configuration template was used —
/// the measurement-partner template is documented as v2-only, while the standard
/// flow is v3. Getting it wrong changes the payload Meta posts back, so it has to be
/// switchable from config rather than requiring a front-end redeploy.
/// </param>
public record WhatsAppOnboardingConfig(
    bool Enabled, string? AppId, string? ConfigId, string GraphVersion, string SessionInfoVersion);

public record WhatsAppOnboardingResult(string PhoneNumberId, string? DisplayPhoneNumber, string WabaId);
