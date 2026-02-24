using Aonik.Platform.Contracts.Api.Seeding;
using Aonik.Platform.Contracts.Services.Seeding;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Permissions;

internal class SeedTenantPermissionsEndpoint : EndpointWithoutRequest<PermissionSeedResponse>
{
    private readonly IPermissionSeedService _permissionSeedService;

    public SeedTenantPermissionsEndpoint(IPermissionSeedService permissionSeedService)
    {
        _permissionSeedService = permissionSeedService;
    }

    public override void Configure()
    {
        Post("/admin/tenants/{tenantId}/permissions/seed");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        var result = await _permissionSeedService.SeedAsync(tenantId, ct);

        var response = new PermissionSeedResponse(
            result.TenantId,
            result.SeededAt,
            result.Operations);

        await Send.OkAsync(response, ct);
    }
}
