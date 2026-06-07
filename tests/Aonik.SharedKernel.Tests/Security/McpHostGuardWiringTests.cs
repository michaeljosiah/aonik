using Aonik.SharedKernel.Security;

using FluentAssertions;

namespace Aonik.SharedKernel.Tests.Security;

/// <summary>
/// Backend review finding C4 — pins that the real MCP host entrypoints actually invoke the
/// fail-closed <see cref="DevelopmentOnlyHostGuard"/> before they build and serve. The blanket-trust
/// stubs those hosts register (grant-all permissions, auto-clear compliance, fixed PlatformAdmin
/// identity) are safe only because the host refuses to start outside Development.
///
/// <see cref="DevelopmentOnlyHostGuardTests"/> covers the guard's throwing behavior in isolation;
/// this covers the *wiring*. Without it, deleting the guard call from a host's Program.cs would
/// leave the suite green while silently re-opening the open-door risk.
/// </summary>
public class McpHostGuardWiringTests
{
    private static readonly string GuardCall =
        $"{nameof(DevelopmentOnlyHostGuard)}.{nameof(DevelopmentOnlyHostGuard.EnsureDevelopmentOnly)}(";

    [Theory]
    [InlineData("src/Aonik.Finance.Mcp/Program.cs")]
    [InlineData("src/Aonik.Platform.Mcp/Program.cs")]
    public void McpHostProgram_Should_InvokeDevelopmentOnlyGuard_BeforeBuildingHost(string relativeProgramPath)
    {
        var programPath = Path.Combine(
            RepositoryRoot(),
            relativeProgramPath.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(programPath).Should().BeTrue(
            "the MCP host entrypoint should exist at {0}", relativeProgramPath);

        var source = File.ReadAllText(programPath);

        var guardIndex = source.IndexOf(GuardCall, StringComparison.Ordinal);
        var buildIndex = source.IndexOf(".Build()", StringComparison.Ordinal);

        guardIndex.Should().BeGreaterThanOrEqualTo(0,
            "the host must call {0} so it fails closed outside Development (finding C4)", GuardCall);
        buildIndex.Should().BeGreaterThanOrEqualTo(0,
            "the host builds its application host with .Build()");
        guardIndex.Should().BeLessThan(buildIndex,
            "the guard must run before the host is built and served, so an insecure host never starts");
    }

    /// <summary>Walks up from the test assembly location to the directory containing Aonik.sln.</summary>
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aonik.sln")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull(
            "the test must run from within the repository so it can read the MCP host sources");
        return dir!.FullName;
    }
}
