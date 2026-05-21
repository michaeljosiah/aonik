using Aonik.Platform.Contracts.Services.Authentication;

namespace Aonik.Infrastructure.Authentication.TokenExchange;

public class AuthTokenServiceFactory : IAuthTokenServiceFactory
{
    private readonly Auth0AuthTokenService _auth0Service;
    private readonly AzureAdAuthTokenService _azureAdService;
    private readonly KeycloakAuthTokenService _keycloakService;

    public AuthTokenServiceFactory(
        Auth0AuthTokenService auth0Service,
        AzureAdAuthTokenService azureAdService,
        KeycloakAuthTokenService keycloakService)
    {
        _auth0Service = auth0Service;
        _azureAdService = azureAdService;
        _keycloakService = keycloakService;
    }

    public IAuthTokenService GetService(string provider)
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
