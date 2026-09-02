using Aonik.Platform.Contracts.Api.Modules;
using Aonik.Platform.Contracts.Models.Modules;
using Aonik.Platform.Contracts.Services.Modules;

using FastEndpoints;

using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

/// <summary>
/// GET /admin/tenants/{tenantId}/modules (Spec 097 §9): every catalogue module with the tenant's
/// resolved state. Host and tenant admins may read; a tenant admin only their own tenant (the
/// service applies the same guard the feature endpoints use).
/// </summary>
internal class GetTenantModulesEndpoint : EndpointWithoutRequest<TenantModuleListResponse>
{
    private readonly ITenantModuleService _tenantModuleService;

    public GetTenantModulesEndpoint(ITenantModuleService tenantModuleService)
    {
        _tenantModuleService = tenantModuleService;
    }

    public override void Configure()
    {
        Get("/admin/tenants/{tenantId}/modules");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get tenant modules";
            s.Description = "Returns every catalogue module with its enabled state, provenance and dependencies for the specified tenant. Core modules are always enabled.";
            s.Response(200, "Module list");
            s.Response(401, "Not authenticated");
            s.Response(403, "Not permitted to read this tenant");
            s.Response(404, "Tenant not found");
        });
        Options(x => x.WithTags("Tenant Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        var result = await _tenantModuleService.GetAsync(tenantId, ct);

        await Send.OkAsync(ToResponse(result), ct);
    }

    internal static TenantModuleListResponse ToResponse(TenantModuleList list)
        => new(
            list.TenantId,
            list.Modules.Select(module => new TenantModuleItemResponse(
                module.ModuleId,
                module.Name,
                module.Description,
                module.IsCore,
                module.DependsOn,
                module.SoftDependsOn,
                module.IsEnabled,
                module.Source,
                module.Reason,
                module.UpdatedAt,
                module.UpdatedBy)).ToList());
}
