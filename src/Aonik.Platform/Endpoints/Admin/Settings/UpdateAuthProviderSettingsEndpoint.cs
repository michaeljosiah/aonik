using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal class UpdateAuthProviderSettingsEndpoint : Endpoint<AuthProviderSettingsUpdateRequest, AuthProviderSettingsResponse>
{
    private readonly IAuthProviderSettingsService _service;

    public UpdateAuthProviderSettingsEndpoint(IAuthProviderSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("/admin/settings/auth-provider");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(AuthProviderSettingsUpdateRequest req, CancellationToken ct)
    {
        var update = new AuthProviderSettingsUpdate(
            req.ActiveProvider,
            req.Auth0 == null
                ? null
                : new Auth0SettingsUpdate(
                    req.Auth0.Domain,
                    req.Auth0.Audience,
                    req.Auth0.ClientId,
                    req.Auth0.ClientSecret,
                    req.Auth0.Connection,
                    req.Auth0.ManagementAudience),
            req.AzureAd == null
                ? null
                : new AzureAdSettingsUpdate(
                    req.AzureAd.Authority,
                    req.AzureAd.Audience,
                    req.AzureAd.ClientId,
                    req.AzureAd.ClientSecret,
                    req.AzureAd.TenantId,
                    req.AzureAd.UserPrincipalNameDomain));

        var snapshot = await _service.UpdateAsync(update, ct);
        await Send.OkAsync(GetAuthProviderSettingsEndpoint.MapResponse(snapshot), ct);
    }
}
