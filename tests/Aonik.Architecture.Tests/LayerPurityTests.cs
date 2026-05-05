using System.Reflection;

using NetArchTest.Rules;

namespace Aonik.Architecture.Tests;

/// <summary>
/// Asserts the layering rules inside each module: entities stay anemic,
/// services live in the Services namespace, and DbContexts are not leaked
/// out of the Persistence layer to higher tiers.
/// </summary>
public class LayerPurityTests
{
    private static readonly string[] ModuleAssemblies =
    [
        "Aonik.Platform",
        "Aonik.Finance",
        "Aonik.Ai",
        "Aonik.Agents",
    ];

    [Fact]
    public void Entities_Should_NotDependOn_EntityFrameworkCore()
    {
        // Aonik's entity policy (CLAUDE.md): "Domain entities are anemic —
        // simple data containers with { get; set; } properties." That means
        // they MUST NOT touch EF Core directly; mapping lives in
        // Persistence/Configurations/IEntityTypeConfiguration<T>.
        foreach (var assemblyName in ModuleAssemblies)
        {
            var assembly = Assembly.Load(assemblyName);

            var result = Types.InAssembly(assembly)
                .That()
                .ResideInNamespaceMatching(@".*\.Entities(\..*)?$")
                .Should()
                .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult();

            AssertNoFailures(result,
                $"{assemblyName} entities must not reference EF Core. " +
                "Mapping belongs in Persistence/Configurations.");
        }
    }

    [Fact]
    public void Entities_Should_NotDependOn_AspNetCore()
    {
        // Same anemic-entity policy: domain entities must not know about
        // HTTP, FastEndpoints, or any other ASP.NET surface.
        foreach (var assemblyName in ModuleAssemblies)
        {
            var assembly = Assembly.Load(assemblyName);

            var result = Types.InAssembly(assembly)
                .That()
                .ResideInNamespaceMatching(@".*\.Entities(\..*)?$")
                .Should()
                .NotHaveDependencyOnAny(
                    "Microsoft.AspNetCore",
                    "FastEndpoints")
                .GetResult();

            AssertNoFailures(result,
                $"{assemblyName} entities must not reference ASP.NET / FastEndpoints. " +
                "HTTP concerns live in the Endpoints layer.");
        }
    }

    [Fact]
    public void Contracts_Should_NotDependOn_EntityFrameworkCore()
    {
        // Contracts (DTOs + service interfaces) sit at the public boundary
        // of each module. They MUST NOT leak EF Core types into request /
        // response shapes — the implementation hides DbContext entirely.
        foreach (var assemblyName in ModuleAssemblies)
        {
            var assembly = Assembly.Load(assemblyName);

            var result = Types.InAssembly(assembly)
                .That()
                .ResideInNamespaceMatching(@".*\.Contracts(\..*)?$")
                .Should()
                .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult();

            AssertNoFailures(result,
                $"{assemblyName} Contracts must not reference EF Core. " +
                "DTOs are pure records / classes with no persistence ties.");
        }
    }

    [Fact]
    public void Contracts_Should_NotDependOn_AspNetCore()
    {
        // Contracts are pure DTOs and service interfaces; they must NOT
        // reference Microsoft.AspNetCore directly (HttpContext, IFormFile,
        // Result, etc.). FastEndpoints binding attributes such as
        // <c>[QueryParam]</c> ARE permitted on request DTOs because the
        // codebase convention places request shapes in Contracts alongside
        // the response DTOs and binding hints.
        foreach (var assemblyName in ModuleAssemblies)
        {
            var assembly = Assembly.Load(assemblyName);

            var result = Types.InAssembly(assembly)
                .That()
                .ResideInNamespaceMatching(@".*\.Contracts(\..*)?$")
                .Should()
                .NotHaveDependencyOn("Microsoft.AspNetCore")
                .GetResult();

            AssertNoFailures(result,
                $"{assemblyName} Contracts must not reference Microsoft.AspNetCore. " +
                "FastEndpoints binding attributes are allowed.");
        }
    }

    [Fact]
    public void DbContexts_Should_LiveIn_PersistenceNamespace()
    {
        // A misplaced DbContext (e.g. in Services or Endpoints) is a strong
        // signal that someone introduced cross-layer leaks. Every concrete
        // DbContext type must reside in a *.Persistence(.…) namespace.
        foreach (var assemblyName in ModuleAssemblies)
        {
            var assembly = Assembly.Load(assemblyName);

            var result = Types.InAssembly(assembly)
                .That()
                .Inherit(typeof(Microsoft.EntityFrameworkCore.DbContext))
                .Should()
                .ResideInNamespaceMatching(@".*\.Persistence(\..*)?$")
                .GetResult();

            AssertNoFailures(result,
                $"{assemblyName} DbContexts must live under a *.Persistence namespace.");
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
