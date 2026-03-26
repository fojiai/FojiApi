namespace FojiApi.Core.Interfaces.Services;

public interface IPlanEnforcementService
{
    Task EnsureCanCreateAgentAsync(int companyId);
    Task EnsureCanEnableWhatsAppAsync(int companyId);
    Task EnsureCanUseEscalationContactsAsync(int companyId);
    Task EnsureHasActiveSubscriptionAsync(int companyId);
    void EnsureFileSizeAllowed(long fileSizeBytes);
}
