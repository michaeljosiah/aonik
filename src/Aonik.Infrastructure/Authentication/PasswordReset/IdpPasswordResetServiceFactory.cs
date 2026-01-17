using Aonik.Application.Abstractions.Authentication;

namespace Aonik.Infrastructure.Authentication.PasswordReset;

public class IdpPasswordResetServiceFactory : IIdpPasswordResetServiceFactory
{
    private readonly Auth0PasswordResetService _auth0Service;
    private readonly AzureAdB2cPasswordResetService _azureAdService;

    public IdpPasswordResetServiceFactory(
        Auth0PasswordResetService auth0Service,
        AzureAdB2cPasswordResetService azureAdService)
    {
        _auth0Service = auth0Service;
        _azureAdService = azureAdService;
    }

    public IIdpPasswordResetService GetService(string provider)
    {
        return provider switch
        {
            "Auth0" => _auth0Service,
            "AzureAd" => _azureAdService,
            _ => throw new InvalidOperationException($"Unsupported provider: {provider}")
        };
    }
}
