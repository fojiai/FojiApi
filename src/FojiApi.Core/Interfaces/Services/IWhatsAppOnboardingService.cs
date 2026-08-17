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
}

/// <param name="Enabled">False when AppId/ConfigId are unset — the UI then offers manual setup.</param>
public record WhatsAppOnboardingConfig(bool Enabled, string? AppId, string? ConfigId, string GraphVersion);

public record WhatsAppOnboardingResult(string PhoneNumberId, string? DisplayPhoneNumber, string WabaId);
