using Aonik.Platform.Contracts.Api.Modules;
using Aonik.Platform.Contracts.Models.Modules;
using Aonik.Platform.Contracts.Services.Modules;

using FastEndpoints;

using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

/// <summary>
/// PUT /admin/tenants/{tenantId}/modules (Spec 097 §9): host admins only. Applies the toggles as one
/// transaction; a dependency violation surfaces as 409 with a typed code (see
/// <see cref="Aonik.SharedKernel.Modules.ModuleErrorCodes"/>), never as a silent cascade.
/// </summary>
internal class UpdateTenantModulesEndpoint : Endpoint<TenantModuleUpdateRequest, TenantModuleListResponse>
{
    private readonly ITenantModuleService _tenantModuleService;

    public UpdateTenantModulesEndpoint(ITenantModuleService tenantModuleService)
    {
        _tenantModuleService = tenantModuleService;
    }

    public override void Configure()
    {
        Put("/admin/tenants/{tenantId}/modules");
        Policies("PlatformAdmin");
        Summary(s =>
        {
            s.Summary = "Update tenant modules";
            s.Description = "Enables or disables non-core modules for the specified tenant. Rejects a toggle that would leave a hard dependency unmet (409 module.dependency_missing) or an enabled dependent stranded (409 module.dependents_enabled). Host administrators only.";
            s.Response(200, "Updated module list");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(403, "Host administrator required");
            s.Response(404, "Tenant not found");
            s.Response(409, "Dependency conflict");
            s.Response(422, "Validation failed");
        });
        Options(x => x.WithTags("Tenant Administration"));
    }

    public override async Task HandleAsync(TenantModuleUpdateRequest req, CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        var toggles = req.Modules
            .Select(module => new TenantModuleToggle(module.ModuleId, module.IsEnabled, module.Reason))
            .ToList();

        TenantModuleList result;
        try
        {
            result = await _tenantModuleService.UpdateAsync(tenantId, toggles, ct);
        }
        catch (ArgumentException exception)
        {
            AddError(exception.Message);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        await Send.OkAsync(GetTenantModulesEndpoint.ToResponse(result), ct);
    }
}
