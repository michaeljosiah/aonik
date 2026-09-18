using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

using FastEndpoints;

using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Manifest;

/// <summary>
/// Runtime module manifest that controls which UI modules, features, routes,
/// and navigation items are visible for the current tenant/user context (Spec 097 §8).
/// </summary>
/// <param name="EnabledModules">Canonical backend module ids that resolved enabled for the tenant, sorted.</param>
/// <param name="Modules">The whole catalogue projected with state, so the UI can explain a disabled route without a second call.</param>
public record AdminManifestResponse(
    string[] EnabledModules,
    IReadOnlyList<ManifestModuleResponse> Modules,
    Dictionary<string, bool> FeatureFlags,
    string[] DisabledRoutes,
    string[] DisabledNavItems);

/// <summary>One catalogue module as the manifest reports it.</summary>
public record ManifestModuleResponse(
    string Id,
    string Name,
    string Description,
    bool IsCore,
    bool IsEnabled,
    IReadOnlyList<string> DependsOn);

/// <summary>
/// GET /admin/manifest — returns the runtime module manifest for the resolved tenant.
/// The Admin UI fetches this on startup (with the bearer token and the selected tenant's
/// X-Tenant-Id) and again on tenant switch and after a module toggle, merging it with its
/// build-time module registry. <c>EnabledModules</c> and <c>Modules</c> come from the tenant's
/// module enablement; feature flags, disabled routes and disabled nav items keep their current
/// hard-coded values until the tenant feature store is wired in.
/// </summary>
internal class GetAdminManifestEndpoint : EndpointWithoutRequest<AdminManifestResponse>
{
    private readonly IModuleEnablementReader _moduleEnablementReader;
    private readonly ITenantProvider _tenantProvider;

    public GetAdminManifestEndpoint(IModuleEnablementReader moduleEnablementReader, ITenantProvider tenantProvider)
    {
        _moduleEnablementReader = moduleEnablementReader;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Get("/admin/manifest");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get admin UI manifest";
            s.Description = "Returns the runtime module manifest for the resolved tenant: the enabled backend module ids, the catalogue with state, and the UI feature flags, disabled routes and disabled navigation items.";
            s.Response(200, "Admin manifest");
            s.Response(400, "Tenant could not be resolved");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new { error = "The tenant for this request could not be resolved.", code = "tenant.not_resolved" },
                ct);
            return;
        }

        var enablement = await _moduleEnablementReader.GetAsync(tenantId, ct);

        var modules = ModuleCatalog.All
            .Select(descriptor => new ManifestModuleResponse(
                descriptor.Id,
                descriptor.Name,
                descriptor.Description,
                descriptor.IsCore,
                enablement.IsEnabled(descriptor.Id),
                descriptor.DependsOn))
            .ToList();

        // Feature flags, disabled routes and disabled nav items keep today's values; wiring
        // featureFlags to the tenant feature store is the within-module follow-up (Spec 097 §8).
        var response = new AdminManifestResponse(
            EnabledModules: enablement.Enabled.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            Modules: modules,
            FeatureFlags: new Dictionary<string, bool>
            {
                ["finance:billing"] = true,
                ["finance:payments"] = true,
                ["finance:ledger"] = true,
                ["finance:orders"] = true,
                ["platform:tenants"] = true,
                ["platform:cms"] = true,
                ["core:ai"] = true,
                ["core:workspace"] = true,
                ["agent-command-center:approvals"] = true,
                ["agent-command-center:run-queue"] = true,
                ["agent-command-center:policies"] = true,
                ["agent-command-center:usage"] = true,
            },
            DisabledRoutes: [],
            DisabledNavItems: []);

        await Send.OkAsync(response, ct);
    }
}
