using Aonik.Api.Contracts.Settings;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Settings;
using Aonik.Application.Settings;
using Aonik.Domain.Settings;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Settings;

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
