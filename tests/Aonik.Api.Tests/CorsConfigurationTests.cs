using Aonik.Api.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aonik.Api.Tests;

/// <summary>
/// Pins the M12 hardening: the literal <c>"null"</c> origin (needed by the
/// Electron desktop, but a spoofing avenue alongside credentials) is off by
/// default and only enabled via <c>Cors:AllowDesktopNullOrigin</c>.
/// </summary>
public class CorsConfigurationTests
{
    private static IList<string> ResolveAonikCorsOrigins(bool? allowDesktopNullOrigin)
    {
        var settings = new Dictionary<string, string?>();
        if (allowDesktopNullOrigin.HasValue)
        {
            settings["Cors:AllowDesktopNullOrigin"] = allowDesktopNullOrigin.Value.ToString();
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddAonikCors(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CorsOptions>>().Value;
        var policy = options.GetPolicy(CorsConfiguration.PolicyName);

        policy.Should().NotBeNull("AddAonikCors must register the {0} policy", CorsConfiguration.PolicyName);
        return policy!.Origins;
    }

    [Fact]
    public void AonikCors_Should_NotAllowNullOrigin_ByDefault()
    {
        var origins = ResolveAonikCorsOrigins(allowDesktopNullOrigin: null);

        origins.Should().NotContain(
            "null",
            "the null origin weakens credentialed CORS and must be off unless a deployment opts in (M12)");
        // Sanity: the rest of the policy is intact.
        origins.Should().Contain("http://localhost:5173", "local-dev origins are always allowed");
    }

    [Fact]
    public void AonikCors_Should_NotAllowNullOrigin_WhenFlagFalse()
    {
        ResolveAonikCorsOrigins(allowDesktopNullOrigin: false).Should().NotContain("null");
    }

    [Fact]
    public void AonikCors_Should_AllowNullOrigin_WhenExplicitlyEnabled()
    {
        ResolveAonikCorsOrigins(allowDesktopNullOrigin: true).Should().Contain(
            "null",
            "deployments that ship the Electron desktop opt in via Cors:AllowDesktopNullOrigin=true");
    }
}
