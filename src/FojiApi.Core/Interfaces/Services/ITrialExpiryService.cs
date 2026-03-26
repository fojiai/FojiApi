namespace FojiApi.Core.Interfaces.Services;

/// <summary>
/// Handles daily trial lifecycle tasks:
///  1. Send reminder emails to companies whose trial ends in 3 days (or 1 day).
///  2. Expire subscriptions whose trial period has ended.
/// </summary>
public interface ITrialExpiryService
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
