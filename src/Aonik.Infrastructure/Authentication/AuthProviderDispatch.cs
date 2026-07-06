namespace Aonik.Infrastructure.Authentication;

/// <summary>
/// Centralizes the "Auth.Provider string -> one of {Auth0, AzureAd, Keycloak}" dispatch
/// that each of the auth capability factories (Spec 029 — JWT validation, IdP management
/// client, user provisioning, password reset, account service, token exchange) hand-rolled
/// as an identical switch. Adding a fourth IdP now touches one mapping here per call site
/// (plus each capability's own per-provider implementation, by design — the six-interface
/// split itself is intentional and unaffected).
/// </summary>
public static class AuthProviderDispatch
{
    public const string Auth0 = "Auth0";
    public const string AzureAd = "AzureAd";
    public const string Keycloak = "Keycloak";

    /// <summary>
    /// Resolves one of three provider-specific values, throwing for an unrecognized provider.
    /// </summary>
    public static T ResolveByProvider<T>(string provider, T auth0Value, T azureAdValue, T keycloakValue)
        => provider switch
        {
            Auth0 => auth0Value,
            AzureAd => azureAdValue,
            Keycloak => keycloakValue,
            _ => throw new InvalidOperationException($"Unsupported auth provider: {provider}")
        };

    /// <summary>
    /// Resolves one of three provider-specific values, returning <c>null</c> for an
    /// unrecognized provider instead of throwing — for call sites that must degrade
    /// gracefully (e.g. no IdP-side management integration configured) rather than fail.
    /// </summary>
    public static T? TryResolveByProvider<T>(string provider, T auth0Value, T azureAdValue, T keycloakValue)
        where T : class
        => provider switch
        {
            Auth0 => auth0Value,
            AzureAd => azureAdValue,
            Keycloak => keycloakValue,
            _ => null
        };
}
