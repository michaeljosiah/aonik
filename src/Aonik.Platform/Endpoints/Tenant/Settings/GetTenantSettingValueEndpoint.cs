using Aonik.Platform.Contracts.Api.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.Platform.Entities.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tenant.Settings;

public class GetTenantSettingValueEndpoint : Endpoint<SettingKeyRequest, SettingValueResponse>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ISettingProvider _settingProvider;

    public GetTenantSettingValueEndpoint(ITenantProvider tenantProvider, ISettingProvider settingProvider)
    {
        _tenantProvider = tenantProvider;
        _settingProvider = settingProvider;
    }

    public override void Configure()
    {
        Get("/tenant/settings/values/{key}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get tenant-scoped setting value";
            s.Description = "Retrieves the tenant-level override value for a specific setting key.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
            s.Response(404, "Setting key not found");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(SettingKeyRequest req, CancellationToken ct)
    {
        if (SettingDefinitions.Get(req.Key) == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var value = await _settingProvider.GetForScopeAsync(req.Key, SettingScope.Tenant, tenantId, cancellationToken: ct);
        await Send.OkAsync(new SettingValueResponse(req.Key, value, "Tenant"), ct);
    }
}
