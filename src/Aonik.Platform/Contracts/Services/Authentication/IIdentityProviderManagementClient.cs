namespace Aonik.Platform.Contracts.Services.Authentication;

/// <summary>
/// Spec 026 Part 2 — abstraction over the IdP management API used to
/// hard-delete a user record outside the platform. Implementations
/// acquire an elevated client-credentials token, call the provider's
/// "delete user" endpoint, and return a structured result. Failures
/// are reported, not thrown, so the calling delete pipeline can
/// continue with the platform-side cleanup and surface the IdP
/// failure to the operator in the response payload.
/// </summary>
public interface IIdentityProviderManagementClient
{
    /// <summary>
    /// Provider key used to select this client. Matches the values
    /// returned by the auth settings provider (e.g. "Auth0", "AzureAd").
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// Delete the user record identified by <paramref name="externalSubject"/>
    /// at the IdP. Returns <c>true</c> on a successful delete (HTTP 204
    /// / 404 — already absent counts as success), <c>false</c> with
    /// <paramref name="failureReason"/> populated on any other outcome.
    /// </summary>
    Task<IdpDeleteUserResult> DeleteUserAsync(
        string externalSubject,
        string? externalTenantId,
        CancellationToken cancellationToken = default);
}

public sealed record IdpDeleteUserResult(
    bool Deleted,
    string? FailureReason);

public interface IIdentityProviderManagementClientFactory
{
    /// <summary>
    /// Resolve a client for the provider currently configured at the
    /// platform level (looks up <c>Auth:Provider</c> from settings).
    /// </summary>
    Task<IIdentityProviderManagementClient?> GetClientAsync(
        CancellationToken cancellationToken = default);
}
