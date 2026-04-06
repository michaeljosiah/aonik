using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Settings;

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
        Summary(s =>
        {
            s.Summary = "Get public auth provider settings";
            s.Description = "Returns the active authentication provider configuration (Auth0 or Azure AD) for client-side use. No authentication required.";
            s.Response(200, "Success");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var snapshot = await _service.GetAsync(ct);
        await Send.OkAsync(MapResponse(snapshot), ct);
    }

    private static PublicAuthProviderSettingsResponse MapResponse(Aonik.Platform.Contracts.Models.Settings.AuthProviderSettingsSnapshot snapshot)
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
