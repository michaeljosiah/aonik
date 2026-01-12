using Aonik.Api.Contracts.Settings;
using Aonik.Application.Services.Settings;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Settings;

public class GetPublicAuthProviderSettingsEndpoint : EndpointWithoutRequest<PublicAuthProviderSettingsResponse>
{
    private readonly IAuthProviderSettingsService _service;

    public GetPublicAuthProviderSettingsEndpoint(IAuthProviderSettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/v1/settings/auth-provider");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var snapshot = await _service.GetAsync(ct);
        await Send.OkAsync(MapResponse(snapshot), ct);
    }

    private static PublicAuthProviderSettingsResponse MapResponse(Application.Models.Settings.AuthProviderSettingsSnapshot snapshot)
    {
        return new PublicAuthProviderSettingsResponse(
            snapshot.ActiveProvider,
            new PublicAuth0SettingsResponse(
                snapshot.Auth0.Domain,
                snapshot.Auth0.Audience,
                snapshot.Auth0.ClientId,
                snapshot.Auth0.Connection),
            new PublicAzureAdSettingsResponse(
                snapshot.AzureAd.Authority,
                snapshot.AzureAd.Audience,
                snapshot.AzureAd.ClientId,
                snapshot.AzureAd.TenantId));
    }
}
