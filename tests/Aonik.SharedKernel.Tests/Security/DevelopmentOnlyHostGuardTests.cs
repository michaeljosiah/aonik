using Aonik.SharedKernel.Security;

using FluentAssertions;

namespace Aonik.SharedKernel.Tests.Security;

/// <summary>
/// Backend review finding C4 — the fail-closed startup guard for MCP hosts that wire
/// development-only blanket-trust stubs (grant-all permissions, auto-clear compliance,
/// fixed PlatformAdmin identity). Pins that the guard admits only the Development
/// environment and fails closed (throws) everywhere else, including when the environment
/// name is unset — the generic host defaults that to Production.
/// </summary>
public class DevelopmentOnlyHostGuardTests
{
    private const string Host = "The Finance MCP server";
    private const string Reason = "It registers a grant-all IPermissionService.";

    [Theory]
    [InlineData("Development")]
    [InlineData("development")]
    [InlineData("DEVELOPMENT")]
    public void EnsureDevelopmentOnly_Should_NotThrow_When_Development(string environmentName)
    {
        var act = () => DevelopmentOnlyHostGuard.EnsureDevelopmentOnly(environmentName, Host, Reason);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Test")]
    [InlineData("Dev")] // deliberately NOT "Development" — only the exact name passes
    public void EnsureDevelopmentOnly_Should_Throw_When_NotDevelopment(string environmentName)
    {
        var act = () => DevelopmentOnlyHostGuard.EnsureDevelopmentOnly(environmentName, Host, Reason);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(environmentName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureDevelopmentOnly_Should_FailClosed_When_EnvironmentNameUnset(string? environmentName)
    {
        // An unset DOTNET_ENVIRONMENT resolves to Production on the generic host, so a
        // missing environment must fail closed rather than be treated as Development.
        var act = () => DevelopmentOnlyHostGuard.EnsureDevelopmentOnly(environmentName, Host, Reason);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EnsureDevelopmentOnly_Should_IncludeHostAndReason_InFailureMessage()
    {
        var act = () => DevelopmentOnlyHostGuard.EnsureDevelopmentOnly("Production", Host, Reason);

        var message = act.Should().Throw<InvalidOperationException>().Which.Message;
        message.Should().Contain(Host, "operators must see which host refused to start");
        message.Should().Contain(Reason, "operators must see why it is development-only");
        message.Should().Contain("DOTNET_ENVIRONMENT=Development", "the message must say how to run it locally");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureDevelopmentOnly_Should_Throw_When_HostDescriptionMissing(string? hostDescription)
    {
        var act = () => DevelopmentOnlyHostGuard.EnsureDevelopmentOnly("Production", hostDescription!, Reason);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("hostDescription");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureDevelopmentOnly_Should_Throw_When_ReasonMissing(string? reason)
    {
        var act = () => DevelopmentOnlyHostGuard.EnsureDevelopmentOnly("Production", Host, reason!);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("reason");
    }
}
