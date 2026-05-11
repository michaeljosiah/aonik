namespace Aonik.Platform.Contracts.Models.Identity;

/// <summary>
/// One tenant the currently-authenticated user has a (User row) membership in.
/// Carries the minimum needed to render an organization picker — no business
/// data, no permissions.
/// </summary>
public record MyTenantSummary(
    Guid TenantId,
    string Name,
    string? Subdomain,
    string Environment
);

/// <summary>
/// Response for the per-user "what tenants do I belong to?" lookup. The
/// only directory-style tenant lookup the API surfaces — public
/// enumeration was retired alongside the old list-for-login endpoint.
/// </summary>
public record MyTenantsResponse(
    List<MyTenantSummary> Tenants
);
