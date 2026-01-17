using Aonik.Application.Abstractions.Authentication;

namespace Aonik.Infrastructure.Authentication.TokenExchange;

public class AuthTokenServiceFactory : IAuthTokenServiceFactory
{
    private readonly Auth0AuthTokenService _auth0Service;
    private readonly AzureAdAuthTokenService _azureAdService;

    public AuthTokenServiceFactory(
        Auth0AuthTokenService auth0Service,
        AzureAdAuthTokenService azureAdService)
    {
        _auth0Service = auth0Service;
        _azureAdService = azureAdService;
    }

    public IAuthTokenService GetService(string provider)
    {
        return provider switch
        {
            "Auth0" => _auth0Service,
            "AzureAd" => _azureAdService,
            _ => throw new InvalidOperationException($"Unsupported provider: {provider}")
        };
    }
}
