using FluentAssertions;

namespace Aonik.SharedKernel.Tests.Modules;

/// <summary>
/// Pins that every composition root registering the Platform module also registers Ordering.
/// Platform's customer admin service takes the SharedKernel <c>IOrderService</c> for the Spec 080
/// registry read-model, and <c>AddOrderingModule</c> is its ONLY registration — so a host that
/// registers Platform without it cannot construct Platform's services. Hosts running in
/// Development validate every registration at <c>Build()</c>, which turns that into a startup
/// failure rather than a first-use one.
/// </summary>
/// <remarks>
/// This gap is invisible to the rest of the suite: the API host is the only composition root the
/// integration tests boot, so the MCP and migrator hosts can break while everything stays green.
/// That is exactly how the dependency shipped broken in the first place — hence a wiring test
/// rather than trusting review to catch the next one.
/// </remarks>
public class PlatformCompositionRootWiringTests
{
    [Theory]
    [InlineData("src/Aonik.Api/Program.cs")]
    [InlineData("src/Aonik.Platform.Mcp/Program.cs")]
    [InlineData("src/Aonik.Migrator/Program.cs")]
    public void HostRegisteringPlatform_Should_AlsoRegisterOrdering(string relativeProgramPath)
    {
        var programPath = Path.Combine(
            RepositoryRoot(),
            relativeProgramPath.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(programPath).Should().BeTrue(
            "the host entrypoint should exist at {0}", relativeProgramPath);

        var source = File.ReadAllText(programPath);

        if (!source.Contains("AddPlatformModule", StringComparison.Ordinal))
        {
            return;   // not a Platform host — nothing to require
        }

        source.Should().Contain(
            "AddOrderingModule",
            "{0} registers Platform, whose customer admin service depends on the SharedKernel " +
            "IOrderService that only AddOrderingModule supplies; without it the host fails to " +
            "build under service validation",
            relativeProgramPath);
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
            "the test must run from within the repository so it can read the host sources");
        return dir!.FullName;
    }
}
