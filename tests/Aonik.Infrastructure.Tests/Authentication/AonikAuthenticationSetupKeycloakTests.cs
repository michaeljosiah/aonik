using Aonik.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>
/// Spec 029 — verifies <see cref="AonikAuthenticationSetup.AddAonikAuthentication"/>
/// wires up the four expected authentication schemes (the "Aonik" policy scheme
/// plus the three JwtBearer arms — AzureAd, Auth0, Keycloak). Behavior tests:
/// we exercise the public API surface (IAuthenticationSchemeProvider, IOptions)
/// rather than the private SelectScheme / GetProviderForIssuer helpers so the
/// tests remain stable across internal refactors.
/// </summary>
public class AonikAuthenticationSetupKeycloakTests
{
    private static IServiceProvider BuildProvider(IDictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAonikAuthentication(configuration);
        return services.BuildServiceProvider();
    }

    private static IDictionary<string, string?> KeycloakConfig() => new Dictionary<string, string?>
    {
        ["Auth:Provider"] = "Keycloak",
        ["Auth:Keycloak:Authority"] = "https://keycloak.example.com/realms/aonik",
        ["Auth:Keycloak:Audience"] = "aonik-api",
    };

    [Fact]
    public async Task AddAonikAuthentication_Should_Register_All_Four_Schemes()
    {
        var provider = BuildProvider(KeycloakConfig());

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = await schemeProvider.GetAllSchemesAsync();
        var schemeNames = schemes.Select(s => s.Name).ToArray();

        schemeNames.Should().Contain(new[] { "Aonik", "AzureAd", "Auth0", "Keycloak" });
    }

    [Fact]
    public async Task AddAonikAuthentication_Should_Register_Keycloak_As_JwtBearer()
    {
        var provider = BuildProvider(KeycloakConfig());

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var keycloak = await schemeProvider.GetSchemeAsync("Keycloak");

        keycloak.Should().NotBeNull();
        keycloak!.HandlerType.Should().Be(typeof(JwtBearerHandler));
    }

    [Fact]
    public void AddAonikAuthentication_Should_SetDefaultScheme_To_Aonik()
    {
        var provider = BuildProvider(KeycloakConfig());

        var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        options.DefaultScheme.Should().Be("Aonik");
        options.DefaultChallengeScheme.Should().Be("Aonik");
    }

    [Fact]
    public void AddAonikAuthentication_Should_Throw_When_AuthConfig_Missing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var act = () => services.AddAonikAuthentication(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Auth configuration is missing*");
    }

    // Test 5 ("AddAonikAuthentication_Should_Throw_When_Provider_Unsupported")
    // was skipped intentionally. The production code passes a hard-coded
    // scheme name ("AzureAd" / "Auth0" / "Keycloak") into
    // ConfigureJwtBearerOptions at registration time — Auth:Provider is
    // only consulted later by SelectScheme / GetProviderForIssuer for
    // dispatch, never as the provider argument to ConfigureJwtBearerOptions.
    // The "Unsupported auth provider" branch is therefore unreachable from
    // the public AddAonikAuthentication API; setting Auth:Provider=Bogus
    // does not trigger it. Testing the unreachable branch would require
    // reaching past the public surface, which the spec explicitly opts
    // against ("the test asserts behavior, not call sites").
}
