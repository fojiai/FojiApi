namespace FojiApi.Core.Interfaces.Services;

public interface IInvitationService
{
    Task<InvitationPreviewResult> GetInvitationAsync(string token);
    Task<AcceptInvitationResult> AcceptInvitationAsync(string token, int userId);
}

public record InvitationPreviewResult(string Email, string CompanyName, string InviterName, string Role, DateTime ExpiresAt);
public record AcceptInvitationResult(string Message, string NewToken);
