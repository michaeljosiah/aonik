using Aonik.Platform.Contracts.Api.Seeding;
using Aonik.Platform.Contracts.Services.Seeding;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Seed tenant permissions";
            s.Description = "Populates the specified tenant with the default set of permission definitions from the platform catalog.";
            s.Response(200, "Permissions seeded");
            s.Response(401, "Not authenticated");
            s.Response(404, "Tenant not found");
        });
        Options(x => x.WithTags("Role Administration"));
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
