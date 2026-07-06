using Aonik.Infrastructure.Authentication;
using Aonik.Platform.Contracts.Services.Authentication;

namespace Aonik.Infrastructure.Authentication.Provisioning;

public class IdpUserProvisionerFactory : IIdpUserProvisionerFactory
{
    private readonly Auth0UserProvisioner _auth0Provisioner;
    private readonly AzureAdUserProvisioner _azureAdProvisioner;
    private readonly KeycloakUserProvisioner _keycloakProvisioner;

    public IdpUserProvisionerFactory(
        Auth0UserProvisioner auth0Provisioner,
        AzureAdUserProvisioner azureAdProvisioner,
        KeycloakUserProvisioner keycloakProvisioner)
    {
        _auth0Provisioner = auth0Provisioner;
        _azureAdProvisioner = azureAdProvisioner;
        _keycloakProvisioner = keycloakProvisioner;
    }

    public IIdpUserProvisioner GetProvisioner(string provider)
        => AuthProviderDispatch.ResolveByProvider<IIdpUserProvisioner>(provider, _auth0Provisioner, _azureAdProvisioner, _keycloakProvisioner);
}
