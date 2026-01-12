using Aonik.Api.Contracts.Settings;
using Aonik.Application.Abstractions.Settings;
using Aonik.Application.Settings;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Settings;

public class GetPublicSettingsEndpoint : EndpointWithoutRequest<List<PublicSettingValueResponse>>
{
    private readonly ISettingProvider _settingProvider;

    public GetPublicSettingsEndpoint(ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public override void Configure()
    {
        Get("/v1/settings/public");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = ResolveTenantId();

        var settings = new List<PublicSettingValueResponse>();
        foreach (var definition in SettingDefinitions.All.Where(def => def.IsVisibleToClients))
        {
            var resolution = await _settingProvider.GetResolvedAsync(definition.Key, tenantId, cancellationToken: ct);
            settings.Add(new PublicSettingValueResponse(definition.Key, resolution.Value));
        }

        await Send.OkAsync(settings, ct);
    }

    private Guid? ResolveTenantId()
    {
        var query = HttpContext.Request.Query["tenantId"].FirstOrDefault();
        return Guid.TryParse(query, out var tenantId) ? tenantId : null;
    }
}
