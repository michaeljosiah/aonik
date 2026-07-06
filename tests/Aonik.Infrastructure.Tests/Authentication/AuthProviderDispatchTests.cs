using Aonik.Infrastructure.Authentication;
using FluentAssertions;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>
/// #226 — the shared provider-string dispatch every auth capability factory
/// now routes through. <see cref="KeycloakFactoryDispatchTests"/> covers the
/// factories themselves end-to-end; these tests pin down the shared helper's
/// own contract in isolation.
/// </summary>
public class AuthProviderDispatchTests
{
    [Theory]
    [InlineData(AuthProviderDispatch.Auth0, "auth0-value")]
    [InlineData(AuthProviderDispatch.AzureAd, "azuread-value")]
    [InlineData(AuthProviderDispatch.Keycloak, "keycloak-value")]
    public void ResolveByProvider_Should_Return_MatchingValue_ForEachKnownProvider(string provider, string expected)
    {
        var result = AuthProviderDispatch.ResolveByProvider(provider, "auth0-value", "azuread-value", "keycloak-value");

        result.Should().Be(expected);
    }

    [Fact]
    public void ResolveByProvider_Should_Throw_ForUnknownProvider()
    {
        var act = () => AuthProviderDispatch.ResolveByProvider("Bogus", "a", "b", "c");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unsupported auth provider: Bogus*");
    }

    [Theory]
    [InlineData(AuthProviderDispatch.Auth0, "auth0-value")]
    [InlineData(AuthProviderDispatch.AzureAd, "azuread-value")]
    [InlineData(AuthProviderDispatch.Keycloak, "keycloak-value")]
    public void TryResolveByProvider_Should_Return_MatchingValue_ForEachKnownProvider(string provider, string expected)
    {
        var result = AuthProviderDispatch.TryResolveByProvider(provider, "auth0-value", "azuread-value", "keycloak-value");

        result.Should().Be(expected);
    }

    [Fact]
    public void TryResolveByProvider_Should_ReturnNull_ForUnknownProvider()
    {
        var result = AuthProviderDispatch.TryResolveByProvider("Bogus", "a", "b", "c");

        result.Should().BeNull();
    }
}
