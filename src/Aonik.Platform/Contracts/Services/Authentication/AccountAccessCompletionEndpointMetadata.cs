namespace Aonik.Platform.Contracts.Services.Authentication;

/// <summary>Only the explicit paid-account completion route may authenticate an unprovisioned subject.</summary>
public sealed class AccountAccessCompletionEndpointMetadata
{
    public const string Route = "/identity/account-access/complete";
}
