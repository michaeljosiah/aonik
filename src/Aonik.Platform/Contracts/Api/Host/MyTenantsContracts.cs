namespace Aonik.Platform.Contracts.Api.Host;

/// <summary>
/// Wire shape for one tenant returned by <c>GET /host/me/tenants</c>.
/// </summary>
public record MyTenantSummaryResponse(
    Guid TenantId,
    string Name,
    string? Subdomain,
    string Environment
);

/// <summary>
/// Wire shape for <c>GET /host/me/tenants</c>: the list of tenants the
/// currently-authenticated identity belongs to.
/// </summary>
public record MyTenantsResponseDto(
    List<MyTenantSummaryResponse> Tenants
);
