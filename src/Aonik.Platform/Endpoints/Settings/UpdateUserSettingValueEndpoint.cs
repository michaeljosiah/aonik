using Aonik.Platform.Contracts.Api.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.Platform.Entities.Settings;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Settings;

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
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Update user-scoped setting value";
            s.Description = "Sets or updates the current user's override value for a specific setting key.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Setting key not found");
        });
        Options(x => x.WithTags("Settings"));
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
