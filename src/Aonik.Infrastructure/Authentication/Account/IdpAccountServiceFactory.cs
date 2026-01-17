using Aonik.Application.Abstractions.Authentication;

namespace Aonik.Infrastructure.Authentication.Account;

public class IdpAccountServiceFactory : IIdpAccountServiceFactory
{
    private readonly Auth0AccountService _auth0Service;
    private readonly AzureAdAccountService _azureAdService;

    public IdpAccountServiceFactory(Auth0AccountService auth0Service, AzureAdAccountService azureAdService)
    {
        _auth0Service = auth0Service;
        _azureAdService = azureAdService;
    }

    public IIdpAccountService GetService(string provider)
    {
        return provider switch
        {
            "Auth0" => _auth0Service,
            "AzureAd" => _azureAdService,
            _ => throw new InvalidOperationException($"Unsupported auth provider: {provider}")
        };
    }
}
