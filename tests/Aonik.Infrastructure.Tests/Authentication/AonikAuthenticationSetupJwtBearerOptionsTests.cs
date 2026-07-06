using Aonik.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>
/// #226 — <c>ConfigureJwtBearerOptions</c> now resolves each provider's
/// Authority/Audience/ValidateIssuer/ClockSkew via <see cref="AuthProviderDispatch"/>
/// instead of a hand-rolled if/else chain. <see cref="AonikAuthenticationSetupKeycloakTests"/>
/// only ever asserted on scheme *registration* (names, handler types); these tests pin
/// down the actual per-scheme option *values*, closing a pre-existing coverage gap and
/// proving the dispatch-based refactor is behavior-preserving.
/// </summary>
public class AonikAuthenticationSetupJwtBearerOptionsTests
{
    private static IServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Provider"] = "Keycloak",
                ["Auth:Auth0:Authority"] = "https://auth0.example.com/",
                ["Auth:Auth0:Audience"] = "auth0-api",
                ["Auth:Auth0:ValidateIssuer"] = "false",
                ["Auth:Auth0:ClockSkewSeconds"] = "60",
                ["Auth:AzureAd:Authority"] = "https://login.microsoftonline.com/tenant-id/v2.0",
                ["Auth:AzureAd:Audience"] = "azuread-api",
                ["Auth:AzureAd:ValidateIssuer"] = "true",
                ["Auth:AzureAd:ClockSkewSeconds"] = "120",
                ["Auth:Keycloak:Authority"] = "https://keycloak.example.com/realms/aonik",
                ["Auth:Keycloak:Audience"] = "keycloak-api",
                ["Auth:Keycloak:ValidateIssuer"] = "true",
                ["Auth:Keycloak:ClockSkewSeconds"] = "45",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAonikAuthentication(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ConfigureJwtBearerOptions_Should_Resolve_Auth0Values()
    {
        var provider = BuildProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Auth0");

        options.Authority.Should().Be("https://auth0.example.com/");
        options.Audience.Should().Be("auth0-api");
        options.TokenValidationParameters.ValidateIssuer.Should().BeFalse();
        options.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void ConfigureJwtBearerOptions_Should_Resolve_AzureAdValues()
    {
        var provider = BuildProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("AzureAd");

        options.Authority.Should().Be("https://login.microsoftonline.com/tenant-id/v2.0");
        options.Audience.Should().Be("azuread-api");
        options.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        options.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void ConfigureJwtBearerOptions_Should_Resolve_KeycloakValues()
    {
        var provider = BuildProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("Keycloak");

        options.Authority.Should().Be("https://keycloak.example.com/realms/aonik");
        options.Audience.Should().Be("keycloak-api");
        options.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        options.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void ConfigureJwtBearerOptions_Should_Apply_CommonValidationParameters_ToEveryScheme()
    {
        var provider = BuildProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();

        foreach (var scheme in new[] { "Auth0", "AzureAd", "Keycloak" })
        {
            var options = monitor.Get(scheme);
            options.RequireHttpsMetadata.Should().BeTrue();
            options.TokenValidationParameters.ValidateAudience.Should().BeTrue();
            options.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
            options.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
            options.TokenValidationParameters.RequireExpirationTime.Should().BeTrue();
            options.TokenValidationParameters.RequireSignedTokens.Should().BeTrue();
        }
    }
}
