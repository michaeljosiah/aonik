namespace Aonik.Application.Models.Identity;

/// <summary>
/// Minimal tenant info returned for login dropdown (public endpoint).
/// </summary>
public record TenantListItemForLogin(
    Guid TenantId,
    string Name,
    string? Subdomain,
    string Environment
);

public record TenantListForLoginResponse(
    List<TenantListItemForLogin> Tenants
);
