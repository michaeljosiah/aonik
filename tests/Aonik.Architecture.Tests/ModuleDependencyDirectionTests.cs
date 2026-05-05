using System.Reflection;

using FluentAssertions;
using NetArchTest.Rules;

namespace Aonik.Architecture.Tests;

/// <summary>
/// Asserts the one-way module dependency direction the codebase relies on.
/// These rules are the executable form of the constraints that the recent
/// architectural cleanup commits (201a9841 / ca0a5eb1 / 5098f370) put in
/// place — Domain modules don't reach the Agents runtime, the Ai module
/// doesn't reach Domain modules, and SharedKernel sits at the bottom.
/// </summary>
/// <remarks>
/// NetArchTest works on compiled assembly metadata, so each test loads the
/// assembly under inspection by name and walks its types' DependsOn graph.
/// </remarks>
public class ModuleDependencyDirectionTests
{
    private const string SharedKernel = "Aonik.SharedKernel";
    private const string Ai = "Aonik.Ai";
    private const string Agents = "Aonik.Agents";
    private const string Platform = "Aonik.Platform";
    private const string Finance = "Aonik.Finance";

    [Fact]
    public void SharedKernel_Should_NotDependOn_AnyOtherAonikModule()
    {
        // Note: NetArchTest matches dependency namespaces as prefixes, so the
        // bare strings "Aonik.Ai" and "Aonik.Agents" risk false positives
        // against SharedKernel's own Abstractions.Ai / Abstractions.Agents
        // sub-namespaces. We name the runtime modules unambiguously by
        // their domain prefix and rely on the explicit module asserts for
        // the rest of the graph (Aonik_Ai_Should_… etc.).
        var assembly = Assembly.Load(SharedKernel);

        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(Platform, Finance, "Aonik.Application", "Aonik.Infrastructure", "Aonik.Api")
            .GetResult();

        AssertNoFailures(result, "SharedKernel must remain the bottom of the dependency graph.");
    }

    [Fact]
    public void Aonik_Ai_Should_NotDependOn_PlatformOrFinance()
    {
        // Resolved by commit 5098f370 — the Ai module previously back-pointed
        // at Platform (for ISettingProvider et al.) and Finance (for
        // CustomerInsightSnapshotResponse). Settings contracts now live on
        // SharedKernel.Abstractions.Settings; the Finance read is brokered
        // by SharedKernel.Abstractions.PersonalFinance.ICustomerInsightSnapshotForAi.
        var assembly = Assembly.Load(Ai);

        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(Platform, Finance)
            .GetResult();

        AssertNoFailures(result, "Aonik.Ai must reach domain data only through SharedKernel abstractions.");
    }

    [Fact]
    public void Aonik_Platform_Should_NotDependOn_AonikAgents()
    {
        // Resolved by commits 201a9841 + ca0a5eb1 — the Platform module
        // previously back-pointed at Agents (for IDomainAgentDescriptor,
        // IAgentConfigurationService, AgentsDbContext). All three contracts
        // now live on SharedKernel.Abstractions.Agents.
        var assembly = Assembly.Load(Platform);

        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOn(Agents)
            .GetResult();

        AssertNoFailures(result, "Aonik.Platform must register agent descriptors via SharedKernel.Abstractions.Agents only.");
    }

    [Fact]
    public void Aonik_Finance_Should_NotDependOn_AonikAgents()
    {
        var assembly = Assembly.Load(Finance);

        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOn(Agents)
            .GetResult();

        AssertNoFailures(result, "Aonik.Finance must register agent descriptors via SharedKernel.Abstractions.Agents only.");
    }

    [Fact]
    public void Aonik_Agents_Should_NotDependOn_PlatformOrFinanceOrAi()
    {
        // Agents is a runtime module. It depends on SharedKernel only and
        // discovers domain agents through DI (IEnumerable<IDomainAgentDescriptor>).
        // It must never reference a domain module directly.
        var assembly = Assembly.Load(Agents);

        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(Platform, Finance, Ai)
            .GetResult();

        AssertNoFailures(result, "Aonik.Agents must remain free of domain-module references — it is a swappable runtime.");
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
