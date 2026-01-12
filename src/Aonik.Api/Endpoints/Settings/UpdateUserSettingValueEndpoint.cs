using Aonik.Api.Contracts.Settings;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Settings;
using Aonik.Application.Settings;
using Aonik.Domain.Settings;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Settings;

public class UpdateUserSettingValueEndpoint : Endpoint<SettingValueUpdateRequest, SettingValueResponse>
{
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ISettingManager _settingManager;

    public UpdateUserSettingValueEndpoint(
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        ISettingManager settingManager)
    {
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _settingManager = settingManager;
    }

    public override void Configure()
    {
        Put("/v1/settings/user/{key}");
        Policies("Settings.Write");
    }

    public override async Task HandleAsync(SettingValueUpdateRequest req, CancellationToken ct)
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
        await _settingManager.SetAsync(req.Key, req.Value, SettingScope.User, tenantId, userId, ct);
        await Send.OkAsync(new SettingValueResponse(req.Key, req.Value, "User"), ct);
    }
}
