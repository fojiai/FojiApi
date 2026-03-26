namespace FojiApi.Core.Interfaces.Services;

public interface ISystemAdminInvitationService
{
    Task InviteAsync(int invitedByUserId, string email);
    Task<object> GetInvitationPreviewAsync(string token);
    Task AcceptAsync(string token, string firstName, string lastName, string password);
    Task<object> ListPendingAsync();
    Task RevokeAsync(int invitationId);
}
