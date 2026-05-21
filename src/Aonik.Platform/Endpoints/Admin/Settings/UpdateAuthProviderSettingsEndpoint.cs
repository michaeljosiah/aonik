using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Update auth provider settings";
            s.Description = "Updates the authentication provider configuration for Auth0 and/or Azure AD.";
            s.Response(200, "Settings updated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(AuthProviderSettingsUpdateRequest req, CancellationToken ct)
    {
        try
        {
            var update = new AuthProviderSettingsUpdate(
                req.ActiveProvider,
                req.Auth0 == null
                    ? null
                    : new Auth0SettingsUpdate(
                        req.Auth0.Domain,
                        req.Auth0.Audience,
                        req.Auth0.ClientId,
                        req.Auth0.ManagementClientId,
                        req.Auth0.ManagementClientSecret,
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
                        req.AzureAd.UserPrincipalNameDomain),
                req.Keycloak == null
                    ? null
                    : new KeycloakSettingsUpdate(
                        req.Keycloak.Authority,
                        req.Keycloak.Audience,
                        req.Keycloak.ClientId,
                        req.Keycloak.ClientSecret,
                        req.Keycloak.Realm,
                        req.Keycloak.AdminClientId,
                        req.Keycloak.AdminClientSecret));

            var snapshot = await _service.UpdateAsync(update, ct);
            await Send.OkAsync(GetAuthProviderSettingsEndpoint.MapResponse(snapshot), ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}
