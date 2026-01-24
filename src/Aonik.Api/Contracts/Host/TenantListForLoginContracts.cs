namespace Aonik.Api.Contracts.Host;

/// <summary>
/// Minimal tenant info for login dropdown (public endpoint).
/// </summary>
public record TenantListItemForLoginResponse(
    Guid TenantId,
    string Name,
    string? Subdomain,
    string Environment
);

public record TenantListForLoginResponse(
    List<TenantListItemForLoginResponse> Tenants
);
