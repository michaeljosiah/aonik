namespace Aonik.Platform.Contracts.Models.Identity;

/// <summary>
/// Payload for <c>POST /admin/tenants</c>. The owner fields are
/// required because every tenant must have at least one
/// <c>TenantAdmin</c> identity provisioned at creation time — JIT
/// user creation for arbitrary tenant logins is no longer permitted.
/// </summary>
/// <param name="Name">Display name shown in the tenant picker.</param>
/// <param name="Environment">Lifecycle environment (Dev/Test/Staging/Prod).</param>
/// <param name="DefaultCurrency">Tenant's default ISO 4217 currency.</param>
/// <param name="SupportedCountries">List of ISO 3166-1 alpha-2 country codes the tenant operates in.</param>
/// <param name="OwnerEmail">
/// Email of the customer's first administrator. A pending placeholder
/// User + Party is created for this email and granted <c>TenantAdmin</c>;
/// the first IdP login matching this email links to the placeholder.
/// </param>
/// <param name="OwnerDisplayName">
/// Optional human-readable name for the owner. Used as the placeholder
/// Party's display name; falls back to the email when omitted.
/// </param>
public record CreateTenantRequest(
    string Name,
    string Environment,
    string DefaultCurrency,
    string[] SupportedCountries,
    string OwnerEmail,
    string? OwnerDisplayName = null,
    string[]? SupportedCurrencies = null,
    string[]? AllowedOriginCountries = null,
    string[]? AllowedDestinationCountries = null
);
