using Aonik.Api.Configuration;
using Aonik.Infrastructure.Authentication.Configuration;

using FluentAssertions;
using NSwag;

namespace Aonik.Api.Tests;

/// <summary>
/// Spec 029 — the OpenAPI document has to describe a flow the provider's client
/// actually enables. Keycloak 26 ships with the implicit flow off, and the dev realm
/// (infra/keycloak/realm-export.json) plus the operator runbook keep it off, so
/// advertising implicit there makes Scalar's Authorize action fail at the realm.
/// </summary>
public class SwaggerConfigurationTests
{
    private static SwaggerOptions SwaggerOptions() => new()
    {
        ClientId = "aonik-swagger",
        Scopes = new List<string> { "openid", "profile", "email" }
    };

    [Fact]
    public void CreateBearerSecurityScheme_Should_UseAuthorizationCodeFlow_When_ProviderIsKeycloak()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            Provider = "Keycloak",
            Keycloak = { Authority = "http://localhost:8080/realms/aonik" }
        };

        // Act
        var scheme = SwaggerConfiguration.CreateBearerSecurityScheme(authOptions, SwaggerOptions());

        // Assert
        scheme.Flow.Should().Be(OpenApiOAuth2Flow.AccessCode);
        scheme.Flows.Implicit.Should().BeNull();
        scheme.Flows.AuthorizationCode.Should().NotBeNull();
        scheme.Flows.AuthorizationCode!.AuthorizationUrl
            .Should().Be("http://localhost:8080/realms/aonik/protocol/openid-connect/auth");
        scheme.Flows.AuthorizationCode.TokenUrl
            .Should().Be("http://localhost:8080/realms/aonik/protocol/openid-connect/token");
        scheme.Flows.AuthorizationCode.Scopes.Keys.Should().Contain(new[] { "openid", "profile", "email" });
    }

    [Fact]
    public void CreateBearerSecurityScheme_Should_KeepImplicitFlow_When_ProviderIsAuth0()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            Provider = "Auth0",
            Auth0 = { Authority = "https://aonik.eu.auth0.com/" }
        };

        // Act
        var scheme = SwaggerConfiguration.CreateBearerSecurityScheme(authOptions, SwaggerOptions());

        // Assert
        scheme.Flow.Should().Be(OpenApiOAuth2Flow.Implicit);
        scheme.Flows.AuthorizationCode.Should().BeNull();
        scheme.Flows.Implicit.Should().NotBeNull();
        scheme.Flows.Implicit!.AuthorizationUrl.Should().Be("https://aonik.eu.auth0.com/authorize");
    }

    [Fact]
    public void CreateBearerSecurityScheme_Should_KeepImplicitFlow_When_ProviderIsAzureAd()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            Provider = "AzureAd",
            AzureAd = { Authority = "https://login.microsoftonline.com/tenant/v2.0" }
        };

        // Act
        var scheme = SwaggerConfiguration.CreateBearerSecurityScheme(authOptions, SwaggerOptions());

        // Assert
        scheme.Flow.Should().Be(OpenApiOAuth2Flow.Implicit);
        scheme.Flows.Implicit.Should().NotBeNull();
        scheme.Flows.Implicit!.TokenUrl.Should().Be("https://login.microsoftonline.com/tenant/v2.0/token");
    }

    [Fact]
    public void CreateBearerSecurityScheme_Should_Throw_When_ProviderIsUnknown()
    {
        // Arrange
        var authOptions = new AuthOptions { Provider = "Okta" };

        // Act
        var act = () => SwaggerConfiguration.CreateBearerSecurityScheme(authOptions, SwaggerOptions());

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Okta*");
    }
}
