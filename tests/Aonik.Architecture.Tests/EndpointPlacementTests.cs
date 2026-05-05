using System.Reflection;

using FastEndpoints;

using NetArchTest.Rules;

namespace Aonik.Architecture.Tests;

/// <summary>
/// Asserts that FastEndpoints classes live where the codebase expects to
/// find them. Misplaced endpoints (e.g. an endpoint defined in a Services
/// folder) defeat the discoverability that the vertical-slice layout
/// gives reviewers.
/// </summary>
public class EndpointPlacementTests
{
    private static readonly string[] ModuleAssemblies =
    [
        "Aonik.Platform",
        "Aonik.Finance",
        "Aonik.Ai",
        "Aonik.Agents",
    ];

    [Fact]
    public void Endpoints_Should_LiveIn_EndpointsNamespace()
    {
        // Every FastEndpoints endpoint type must reside under
        // *.Endpoints(.…). The convention is the entry point reviewers
        // follow when tracing an HTTP route to a handler. The matching
        // regex tolerates nested folders (e.g. Endpoints/Admin/Settings).
        foreach (var assemblyName in ModuleAssemblies)
        {
            var assembly = Assembly.Load(assemblyName);

            var result = Types.InAssembly(assembly)
                .That()
                .Inherit(typeof(BaseEndpoint))
                .Should()
                .ResideInNamespaceMatching(@".*\.Endpoints(\..*)?$")
                .GetResult();

            AssertNoFailures(result,
                $"{assemblyName} endpoints must live under a *.Endpoints namespace " +
                "so vertical-slice navigation works.");
        }
    }

    [Fact]
    public void EndpointValidators_Should_LiveIn_EndpointsNamespace()
    {
        // FluentValidation validators paired with a request DTO are a
        // sibling of the endpoint and live next to it under Endpoints.
        // Keeps the request → validator → handler triangle in one folder.
        foreach (var assemblyName in ModuleAssemblies)
        {
            var assembly = Assembly.Load(assemblyName);

            var result = Types.InAssembly(assembly)
                .That()
                .Inherit(typeof(FastEndpoints.Validator<>))
                .Should()
                .ResideInNamespaceMatching(@".*\.Endpoints(\..*)?$")
                .GetResult();

            AssertNoFailures(result,
                $"{assemblyName} request-DTO validators must live under a *.Endpoints namespace " +
                "alongside their endpoints.");
        }
    }

    [Fact]
    public void Endpoints_Should_NotResideIn_ServicesOrEntitiesOrPersistence()
    {
        // Inverse check — anything reachable in Services / Entities /
        // Persistence must NOT inherit from BaseEndpoint. Catches the
        // mirror-image violation from EndpointsLiveInEndpointsNamespace.
        foreach (var assemblyName in ModuleAssemblies)
        {
            var assembly = Assembly.Load(assemblyName);

            var result = Types.InAssembly(assembly)
                .That()
                .ResideInNamespaceMatching(@".*\.(Services|Entities|Persistence)(\..*)?$")
                .Should()
                .NotInherit(typeof(BaseEndpoint))
                .GetResult();

            AssertNoFailures(result,
                $"{assemblyName} services/entities/persistence types must not be FastEndpoints endpoints.");
        }
    }

    private static void AssertNoFailures(TestResult result, string because)
    {
        if (result.IsSuccessful)
            return;

        var failingTypes = result.FailingTypeNames is null
            ? "<none>"
            : string.Join(Environment.NewLine + "  ", result.FailingTypeNames);

        Assert.Fail(
            because + Environment.NewLine +
            "Failing types:" + Environment.NewLine +
            "  " + failingTypes);
    }
}
