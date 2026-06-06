using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Get admin UI manifest";
            s.Description = "Returns the runtime module manifest that controls which UI modules, features, routes, and navigation items are available.";
            s.Response(200, "Admin manifest");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // For now, return all modules enabled with no overrides.
        // Future: resolve from tenant features, user roles, feature flags.
        //
        // NOTE: every module the AdminUi registers must appear in
        // EnabledModules — useModules() filters its build-time aggregation
        // by this list. A missing entry causes the entire nav section to
        // disappear (Approvals / Run queue / Policies / Usage all live in
        // agent-command-center, for example).
        var response = new AdminManifestResponse(
            EnabledModules: ["core", "platform", "finance", "agent-command-center", "agent-extensions"],
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
