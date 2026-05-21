using Aonik.Platform.Contracts.Models.Configuration;

namespace Aonik.Infrastructure.Authentication.Configuration;

public class AuthOptions
{
    public string Provider { get; set; } = "AzureAd"; // "AzureAd", "Auth0", or "Keycloak"
    public TenantRoutingMode TenantRouting { get; set; } = TenantRoutingMode.Claim;
    public AzureAdOptions AzureAd { get; set; } = new();
    public Auth0Options Auth0 { get; set; } = new();
    public KeycloakOptions Keycloak { get; set; } = new();
}

public class AzureAdOptions
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool ValidateIssuer { get; set; } = true;
    public int ClockSkewSeconds { get; set; } = 300;
}

public class Auth0Options
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool ValidateIssuer { get; set; } = true;
    public int ClockSkewSeconds { get; set; } = 300;
}

/// <summary>
/// Spec 029 — Keycloak realm configuration. <see cref="Authority"/> is the full
/// realm URL (e.g. <c>https://keycloak.example.com/realms/aonik</c>); the JwtBearer
/// middleware resolves the JWKS / token endpoints via the realm's OIDC discovery
/// document, so no other endpoint URLs need to live in config.
/// </summary>
public class KeycloakOptions
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool ValidateIssuer { get; set; } = true;
    public int ClockSkewSeconds { get; set; } = 300;
}
