namespace FojiApi.Core.Interfaces.Services;

public interface IInvitationService
{
    Task<InvitationPreviewResult> GetInvitationAsync(string token);
    Task<AcceptInvitationResult> AcceptInvitationAsync(string token, int userId);

    /// <summary>
    /// Creates the invited person's account and joins them to the company in one
    /// step. The invite link was emailed to them, so it proves they own the
    /// address: the account starts verified and they're signed in straight away.
    /// </summary>
    Task<LoginResult> RegisterAndAcceptAsync(string token, string firstName, string lastName, string password);
}

/// <param name="AccountExists">
/// A verified account already uses the invited email — the page asks them to
/// log in instead of creating one.
/// </param>
public record InvitationPreviewResult(
    string Email, int CompanyId, string CompanyName, string InviterName, string Role, DateTime ExpiresAt, bool AccountExists);

public record AcceptInvitationResult(string Message, string NewToken, int CompanyId);
