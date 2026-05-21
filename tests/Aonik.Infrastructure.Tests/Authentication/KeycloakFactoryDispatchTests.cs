using System.Threading;
using System.Threading.Tasks;

using Aonik.Infrastructure.Authentication.Account;
using Aonik.Infrastructure.Authentication.PasswordReset;
using Aonik.Infrastructure.Authentication.Provisioning;
using Aonik.Infrastructure.Authentication.TokenExchange;
using Aonik.Platform.Services.Settings;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>
/// Spec 029 — proves each of the five IdP factories dispatches the
/// "Keycloak" string to the Keycloak* implementation. Cheap insurance
/// against future contributors adding a sixth provider and forgetting
/// to thread it through one of the factories.
/// </summary>
public class KeycloakFactoryDispatchTests
{
    [Fact]
    public async Task IdentityProviderManagementClientFactory_Should_Resolve_Keycloak()
    {
        var settings = new InMemorySettingProvider();
        settings.Set(AuthSettingNames.Provider, "Keycloak");

        var factory = new IdentityProviderManagementClientFactory(
            settings,
            new Auth0ManagementClient(new HttpClient(), settings, NullLogger<Auth0ManagementClient>.Instance),
            new AzureAdManagementClient(new HttpClient(), settings, NullLogger<AzureAdManagementClient>.Instance),
            new KeycloakManagementClient(new HttpClient(), settings, NullLogger<KeycloakManagementClient>.Instance));

        var client = await factory.GetClientAsync(CancellationToken.None);

        client.Should().NotBeNull();
        client!.Provider.Should().Be("Keycloak");
    }

    [Fact]
    public void IdpUserProvisionerFactory_Should_Resolve_Keycloak()
    {
        var settings = new InMemorySettingProvider();
        var factory = new IdpUserProvisionerFactory(
            new Auth0UserProvisioner(new HttpClient(), settings),
            new AzureAdUserProvisioner(new HttpClient(), settings),
            new KeycloakUserProvisioner(new HttpClient(), settings));

        var provisioner = factory.GetProvisioner("Keycloak");

        provisioner.Should().BeOfType<KeycloakUserProvisioner>();
    }

    [Fact]
    public void IdpPasswordResetServiceFactory_Should_Resolve_Keycloak()
    {
        var settings = new InMemorySettingProvider();
        var factory = new IdpPasswordResetServiceFactory(
            new Auth0PasswordResetService(new HttpClient(), settings),
            new AzureAdB2cPasswordResetService(new HttpClient(), settings),
            new KeycloakPasswordResetService(new HttpClient(), settings));

        var service = factory.GetService("Keycloak");

        service.Should().BeOfType<KeycloakPasswordResetService>();
    }

    [Fact]
    public void IdpAccountServiceFactory_Should_Resolve_Keycloak()
    {
        var settings = new InMemorySettingProvider();
        var factory = new IdpAccountServiceFactory(
            new Auth0AccountService(new HttpClient(), settings),
            new AzureAdAccountService(new HttpClient(), settings),
            new KeycloakAccountService(new HttpClient(), settings));

        var service = factory.GetService("Keycloak");

        service.Should().BeOfType<KeycloakAccountService>();
    }

    [Fact]
    public void AuthTokenServiceFactory_Should_Resolve_Keycloak()
    {
        var settings = new InMemorySettingProvider();
        var factory = new AuthTokenServiceFactory(
            new Auth0AuthTokenService(new HttpClient(), settings),
            new AzureAdAuthTokenService(new HttpClient(), settings),
            new KeycloakAuthTokenService(new HttpClient(), settings));

        var service = factory.GetService("Keycloak");

        service.Should().BeOfType<KeycloakAuthTokenService>();
    }

    [Fact]
    public void Factories_Should_Reject_UnknownProvider()
    {
        var settings = new InMemorySettingProvider();
        var factory = new IdpUserProvisionerFactory(
            new Auth0UserProvisioner(new HttpClient(), settings),
            new AzureAdUserProvisioner(new HttpClient(), settings),
            new KeycloakUserProvisioner(new HttpClient(), settings));

        var act = () => factory.GetProvisioner("UnknownProvider");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported provider: UnknownProvider*");
    }
}
