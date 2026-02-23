using Aonik.Platform.Contracts.Api.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.Platform.Entities.Settings;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Settings;

public class GetUserSettingValueEndpoint : Endpoint<SettingKeyRequest, SettingValueResponse>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ISettingProvider _settingProvider;

    public GetUserSettingValueEndpoint(
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        ISettingProvider settingProvider)
    {
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _settingProvider = settingProvider;
    }

    public override void Configure()
    {
        Get("/v1/settings/user/{key}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(SettingKeyRequest req, CancellationToken ct)
    {
        if (SettingDefinitions.Get(req.Key) == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var value = await _settingProvider.GetForScopeAsync(req.Key, SettingScope.User, tenantId, userId, ct);
        await Send.OkAsync(new SettingValueResponse(req.Key, value, "User"), ct);
    }
}
