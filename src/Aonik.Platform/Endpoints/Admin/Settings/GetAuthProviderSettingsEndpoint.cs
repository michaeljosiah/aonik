using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal class GetAuthProviderSettingsEndpoint : EndpointWithoutRequest<AuthProviderSettingsResponse>
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
        Summary(s =>
        {
            s.Summary = "Get auth provider settings";
            s.Description = "Retrieves the current authentication provider configuration including Auth0, Azure AD, and Keycloak settings.";
            s.Response(200, "Auth provider settings");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var snapshot = await _service.GetAsync(ct);
        await Send.OkAsync(MapResponse(snapshot), ct);
    }

    internal static AuthProviderSettingsResponse MapResponse(Aonik.Platform.Contracts.Models.Settings.AuthProviderSettingsSnapshot snapshot)
    {
        return new AuthProviderSettingsResponse(
            snapshot.ActiveProvider,
            new Auth0SettingsResponse(
                snapshot.Auth0.Domain,
                snapshot.Auth0.Audience,
                snapshot.Auth0.ClientId,
                snapshot.Auth0.ManagementClientId,
                snapshot.Auth0.HasManagementClientSecret,
                snapshot.Auth0.Connection,
                snapshot.Auth0.ManagementAudience),
            new AzureAdSettingsResponse(
                snapshot.AzureAd.Authority,
                snapshot.AzureAd.Audience,
                snapshot.AzureAd.ClientId,
                snapshot.AzureAd.HasClientSecret,
                snapshot.AzureAd.TenantId,
                snapshot.AzureAd.UserPrincipalNameDomain),
            new KeycloakSettingsResponse(
                snapshot.Keycloak.Authority,
                snapshot.Keycloak.Audience,
                snapshot.Keycloak.ClientId,
                snapshot.Keycloak.HasClientSecret,
                snapshot.Keycloak.Realm,
                snapshot.Keycloak.AdminClientId,
                snapshot.Keycloak.HasAdminClientSecret));
    }
}
