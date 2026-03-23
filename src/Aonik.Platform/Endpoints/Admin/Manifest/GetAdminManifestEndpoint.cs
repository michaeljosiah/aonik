using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Manifest;

/// <summary>
/// Runtime module manifest that controls which UI modules, features, routes,
/// and navigation items are visible for the current tenant/user context.
/// </summary>
public record AdminManifestResponse(
    string[] EnabledModules,
    Dictionary<string, bool> FeatureFlags,
    string[] DisabledRoutes,
    string[] DisabledNavItems);

/// <summary>
/// GET /admin/manifest — returns the runtime module manifest.
/// The Admin UI fetches this on startup to merge with build-time module configs.
/// </summary>
internal class GetAdminManifestEndpoint : EndpointWithoutRequest<AdminManifestResponse>
{
    public override void Configure()
    {
        Get("/admin/manifest");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // For now, return all modules enabled with no overrides.
        // Future: resolve from tenant features, user roles, feature flags.
        var response = new AdminManifestResponse(
            EnabledModules: ["core", "platform", "finance"],
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
            },
            DisabledRoutes: [],
            DisabledNavItems: []);

        await Send.OkAsync(response, ct);
    }
}
