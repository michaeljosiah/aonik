using Aonik.Agents.Framework;

using FluentAssertions;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Finding C4 (defense-in-depth) — when the parent host is not Development, a spawned MCP child must
/// never resolve its environment to Development, or a first-party Finance/Platform MCP host would pass
/// <c>DevelopmentOnlyHostGuard</c> and run its blanket-trust stubs with real authority. The stdio
/// transport merges these overrides on top of the parent process environment (a null value removes an
/// inherited variable), so the child must be handed explicit nulls to strip an inherited
/// <c>DOTNET_ENVIRONMENT=Development</c> — not merely have a configured override skipped.
/// </summary>
public class McpToolProviderEnvironmentTests
{
    private static McpServerConfig Config(params (string Key, string Value)[] env)
    {
        var config = new McpServerConfig { Name = "finance" };
        foreach (var (key, value) in env)
        {
            config.EnvironmentVariables[key] = value;
        }
        return config;
    }

    [Fact]
    public void BuildChildEnvironmentVariables_Should_StripInheritedEnvironmentSelectors_When_ParentNotDevelopment_AndNoOverrides()
    {
        // No configured overrides: the child must still be prevented from inheriting a
        // DOTNET_ENVIRONMENT=Development the parent process may carry — both selectors are nulled.
        var env = McpToolProvider.BuildChildEnvironmentVariables(Config(), parentIsDevelopment: false);

        env.Should().NotBeNull();
        env!.Should().ContainKey("DOTNET_ENVIRONMENT");
        env["DOTNET_ENVIRONMENT"].Should().BeNull();
        env.Should().ContainKey("ASPNETCORE_ENVIRONMENT");
        env["ASPNETCORE_ENVIRONMENT"].Should().BeNull();
    }

    [Theory]
    [InlineData("DOTNET_ENVIRONMENT")]
    [InlineData("ASPNETCORE_ENVIRONMENT")]
    [InlineData("dotnet_environment")] // matched case-insensitively
    public void BuildChildEnvironmentVariables_Should_DropDevelopmentOverride_When_ParentNotDevelopment(string key)
    {
        var ignored = new List<string>();

        var env = McpToolProvider.BuildChildEnvironmentVariables(
            Config((key, "Development")),
            parentIsDevelopment: false,
            ignored.Add);

        ignored.Should().ContainSingle().Which.Should().Be(key);
        env.Should().NotBeNull();
        env![key].Should().BeNull("a Development override must not survive into the child");
    }

    [Fact]
    public void BuildChildEnvironmentVariables_Should_AllowNonDevelopmentOverride_When_ParentNotDevelopment()
    {
        // A non-Development value is a legitimate override and still fails the guard closed.
        var env = McpToolProvider.BuildChildEnvironmentVariables(
            Config(("DOTNET_ENVIRONMENT", "Staging")),
            parentIsDevelopment: false);

        env!["DOTNET_ENVIRONMENT"].Should().Be("Staging");
    }

    [Fact]
    public void BuildChildEnvironmentVariables_Should_PassUnrelatedVariables_AndStillStripSelectors_When_ParentNotDevelopment()
    {
        var env = McpToolProvider.BuildChildEnvironmentVariables(
            Config(("FOO", "bar")),
            parentIsDevelopment: false);

        env!["FOO"].Should().Be("bar");
        env["DOTNET_ENVIRONMENT"].Should().BeNull();
        env["ASPNETCORE_ENVIRONMENT"].Should().BeNull();
    }

    [Fact]
    public void BuildChildEnvironmentVariables_Should_LeaveDevelopmentChildUntouched_When_ParentIsDevelopment()
    {
        // A Development parent spawning a Development child is the intended local-dev path.
        var ignored = new List<string>();

        var env = McpToolProvider.BuildChildEnvironmentVariables(
            Config(("DOTNET_ENVIRONMENT", "Development")),
            parentIsDevelopment: true,
            ignored.Add);

        ignored.Should().BeEmpty();
        env!["DOTNET_ENVIRONMENT"].Should().Be("Development");
    }

    [Fact]
    public void BuildChildEnvironmentVariables_Should_ReturnNull_When_ParentDevelopment_AndNoConfiguredVariables()
    {
        // Nothing to override — let the child inherit the (Development) parent environment.
        var env = McpToolProvider.BuildChildEnvironmentVariables(Config(), parentIsDevelopment: true);

        env.Should().BeNull();
    }
}
