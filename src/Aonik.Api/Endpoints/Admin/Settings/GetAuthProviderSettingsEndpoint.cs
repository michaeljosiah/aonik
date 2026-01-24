using Aonik.Api.Contracts.Settings;
using Aonik.Application.Services.Settings;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Settings;

public class GetAuthProviderSettingsEndpoint : EndpointWithoutRequest<AuthProviderSettingsResponse>
{
    private readonly IAuthProviderSettingsService _service;

    public GetAuthProviderSettingsEndpoint(IAuthProviderSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/settings/auth-provider");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var snapshot = await _service.GetAsync(ct);
        await Send.OkAsync(MapResponse(snapshot), ct);
    }

    internal static AuthProviderSettingsResponse MapResponse(Application.Models.Settings.AuthProviderSettingsSnapshot snapshot)
    {
        return new AuthProviderSettingsResponse(
            snapshot.ActiveProvider,
            new Auth0SettingsResponse(
                snapshot.Auth0.Domain,
                snapshot.Auth0.Audience,
                snapshot.Auth0.ClientId,
                snapshot.Auth0.HasClientSecret,
                snapshot.Auth0.Connection,
                snapshot.Auth0.ManagementAudience),
            new AzureAdSettingsResponse(
                snapshot.AzureAd.Authority,
                snapshot.AzureAd.Audience,
                snapshot.AzureAd.ClientId,
                snapshot.AzureAd.HasClientSecret,
                snapshot.AzureAd.TenantId,
                snapshot.AzureAd.UserPrincipalNameDomain));
    }
}
