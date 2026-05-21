using Aonik.Platform.Contracts.Services.Authentication;

namespace Aonik.Infrastructure.Authentication.PasswordReset;

public class IdpPasswordResetServiceFactory : IIdpPasswordResetServiceFactory
{
    private readonly Auth0PasswordResetService _auth0Service;
    private readonly AzureAdB2cPasswordResetService _azureAdService;
    private readonly KeycloakPasswordResetService _keycloakService;

    public IdpPasswordResetServiceFactory(
        Auth0PasswordResetService auth0Service,
        AzureAdB2cPasswordResetService azureAdService,
        KeycloakPasswordResetService keycloakService)
    {
        _auth0Service = auth0Service;
        _azureAdService = azureAdService;
        _keycloakService = keycloakService;
    }

    public IIdpPasswordResetService GetService(string provider)
    {
        return provider switch
        {
            "Auth0" => _auth0Service,
            "AzureAd" => _azureAdService,
            "Keycloak" => _keycloakService,
            _ => throw new InvalidOperationException($"Unsupported provider: {provider}")
        };
    }
}
