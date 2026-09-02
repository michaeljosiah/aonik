using System.Reflection;

using Aonik.SharedKernel.Modules;

using FluentAssertions;

namespace Aonik.Architecture.Tests;

/// <summary>
/// Spec 097 §5 / §17: every module assembly carries an <see cref="AonikModuleAttribute"/> whose id is in
/// the <see cref="ModuleCatalog"/>, ids are unique across assemblies, and every <see cref="IModule"/>
/// implementation's <c>Id</c> matches its assembly's attribute. The gate is complete by construction
/// only while this holds — an endpoint cannot be forgotten because it lives in an assembly, but only
/// if the assembly says which module it is.
/// </summary>
public class ModuleAssemblyIdentityTests
{
    /// <summary>
    /// Enumerated by type rather than by scanning AppDomain, so a module assembly that stops being
    /// referenced shows up as a compile error here, not as a silently shorter list.
    /// </summary>
    private static readonly IReadOnlyList<Assembly> ModuleAssemblies =
    [
        typeof(Aonik.Platform.PlatformModule).Assembly,
        typeof(Aonik.Ordering.OrderingModule).Assembly,
        typeof(Aonik.Finance.FinanceModule).Assembly,
        typeof(Aonik.Commerce.CommerceModule).Assembly,
        typeof(Aonik.Subscriptions.SubscriptionsModule).Assembly,
        typeof(Aonik.Groups.GroupsModule).Assembly,
        typeof(Aonik.Workspaces.WorkspacesModule).Assembly,
        typeof(Aonik.PersonalFinance.PersonalFinanceModule).Assembly,
        typeof(Aonik.Ai.AiModule).Assembly,
        typeof(Aonik.Agents.AgentsModule).Assembly,
        typeof(Aonik.Voice.AonikVoiceModule).Assembly,
        typeof(Aonik.Documents.DocumentsModule).Assembly,
    ];

    private static readonly IReadOnlyList<Assembly> NonModuleAssemblies =
    [
        typeof(ModuleCatalog).Assembly,                                   // Aonik.SharedKernel
        typeof(Aonik.Application.Abstractions.Persistence.IAonikDbContext).Assembly, // Aonik.Application
        typeof(Aonik.Infrastructure.Persistence.AonikDbContext).Assembly, // Aonik.Infrastructure
    ];

    [Fact]
    public void EveryModuleAssembly_Should_CarryAnAonikModuleAttributeWithAKnownId()
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var moduleId = ModuleCatalog.TryGetModuleId(assembly);

            moduleId.Should().NotBeNull("{0} must declare [assembly: AonikModule(...)]", assembly.GetName().Name);
            ModuleCatalog.IsKnown(moduleId!).Should().BeTrue(
                "{0} declares module id '{1}', which is not in the catalogue", assembly.GetName().Name, moduleId);
        }
    }

    [Fact]
    public void ModuleIds_Should_BeUniqueAcrossAssemblies_And_CoverTheWholeCatalogue()
    {
        var declared = ModuleAssemblies
            .Select(assembly => ModuleCatalog.TryGetModuleId(assembly)!)
            .ToList();

        declared.Should().OnlyHaveUniqueItems("two assemblies cannot claim the same module");
        declared.Should().BeEquivalentTo(
            ModuleCatalog.All.Select(descriptor => descriptor.Id),
            "every catalogue module has exactly one assembly and every module assembly is in the catalogue");
    }

    [Fact]
    public void EveryIModuleImplementation_Should_ReportTheIdOfItsOwnAssembly()
    {
        var implementations = ModuleAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IModule).IsAssignableFrom(type))
            .ToList();

        implementations.Should().HaveCount(ModuleAssemblies.Count,
            "each of the twelve module assemblies implements IModule exactly once");

        foreach (var implementation in implementations)
        {
            var idProperty = implementation.GetProperty(nameof(IModule.Id), BindingFlags.Public | BindingFlags.Static);
            idProperty.Should().NotBeNull("{0} must expose the static Id from IModule", implementation.Name);

            var declaredId = (string?)idProperty!.GetValue(null);
            var assemblyId = ModuleCatalog.TryGetModuleId(implementation.Assembly);

            declaredId.Should().Be(assemblyId,
                "{0}.Id must match the AonikModule attribute on {1}", implementation.Name, implementation.Assembly.GetName().Name);
        }
    }

    [Fact]
    public void HostAndCompositionAssemblies_Should_CarryNoModuleAttribute()
    {
        // Spec 097 §5: Api, Application, Infrastructure and SharedKernel are never gated, so they
        // must never look like a module to anything resolving identity from Type.Assembly.
        foreach (var assembly in NonModuleAssemblies)
        {
            ModuleCatalog.TryGetModuleId(assembly).Should().BeNull(
                "{0} is a composition assembly, not a module", assembly.GetName().Name);
        }
    }
}
