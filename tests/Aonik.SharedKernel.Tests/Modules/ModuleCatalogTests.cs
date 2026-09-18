using Aonik.SharedKernel.Modules;

using FluentAssertions;

namespace Aonik.SharedKernel.Tests.Modules;

/// <summary>
/// Spec 097 §5 / §7 / §16: the shipped catalogue is valid, its graph is acyclic, and the pure resolver
/// applies defaults, overlays rows, forces core and closes over hard dependencies.
/// </summary>
public class ModuleCatalogTests
{
    private static readonly string[] ExpectedCore =
        [ModuleIds.Platform, ModuleIds.Ordering, ModuleIds.Ai, ModuleIds.Agents];

    private static IReadOnlySet<string> AllIds
        => ModuleCatalog.All.Select(descriptor => descriptor.Id).ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Validate_Should_Pass_When_CatalogueIsTheShippedOne()
    {
        var act = () => ModuleCatalog.Validate();

        act.Should().NotThrow("the shipped catalogue must be acyclic with only known, core-consistent dependencies");
    }

    [Fact]
    public void All_Should_ContainExactlyTheTwelveModuleIds()
    {
        AllIds.Should().BeEquivalentTo(new[]
        {
            ModuleIds.Platform, ModuleIds.Ordering, ModuleIds.Finance, ModuleIds.Commerce,
            ModuleIds.Subscriptions, ModuleIds.Groups, ModuleIds.Workspaces, ModuleIds.PersonalFinance,
            ModuleIds.Ai, ModuleIds.Agents, ModuleIds.Voice, ModuleIds.Documents,
        });
    }

    [Fact]
    public void All_Should_UseKebabCaseIds()
    {
        foreach (var descriptor in ModuleCatalog.All)
        {
            descriptor.Id.Should().MatchRegex("^[a-z]+(-[a-z]+)*$", "module ids are canonical kebab-case");
        }
    }

    [Fact]
    public void EveryDependency_Should_BeAKnownModuleId()
    {
        foreach (var descriptor in ModuleCatalog.All)
        {
            descriptor.DependsOn.Should().OnlyContain(id => ModuleCatalog.IsKnown(id),
                "hard dependencies of {0} must be catalogue ids", descriptor.Id);
            descriptor.SoftDependsOn.Should().OnlyContain(id => ModuleCatalog.IsKnown(id),
                "soft dependencies of {0} must be catalogue ids", descriptor.Id);
        }
    }

    [Fact]
    public void CoreIds_Should_BePlatformOrderingAiAndAgents()
    {
        ModuleCatalog.CoreIds.Should().BeEquivalentTo(ExpectedCore);
        ModuleCatalog.All.Where(descriptor => descriptor.IsCore).Select(descriptor => descriptor.Id)
            .Should().BeEquivalentTo(ExpectedCore);
    }

    [Fact]
    public void CoreModules_Should_NotHardDependOnNonCoreModules()
    {
        foreach (var descriptor in ModuleCatalog.All.Where(descriptor => descriptor.IsCore))
        {
            descriptor.DependsOn.Should().OnlyContain(id => ModuleCatalog.Get(id).IsCore,
                "core module {0} can never be off, so everything it requires must be core too", descriptor.Id);
        }
    }

    [Fact]
    public void EveryModule_Should_DefaultToEnabled()
    {
        // Spec 097 §4 "Defaults": an absent row means on, so existing tenants change nothing on deploy.
        ModuleCatalog.All.Should().OnlyContain(descriptor => descriptor.DefaultEnabled);
    }

    [Fact]
    public void Get_Should_Throw_When_IdIsUnknown()
    {
        var act = () => ModuleCatalog.Get("not-a-module");

        act.Should().Throw<KeyNotFoundException>();
        ModuleCatalog.TryGet("not-a-module").Should().BeNull();
        ModuleCatalog.IsKnown("not-a-module").Should().BeFalse();
    }

    [Fact]
    public void HardDependencyClosure_Should_IncludeInputsAndTransitiveHardDependencies()
    {
        var closure = ModuleCatalog.HardDependencyClosure([ModuleIds.Workspaces]);

        // workspaces -> groups, subscriptions; subscriptions -> ordering, finance; finance -> ordering
        closure.Should().BeEquivalentTo(new[]
        {
            ModuleIds.Workspaces, ModuleIds.Groups, ModuleIds.Subscriptions, ModuleIds.Ordering, ModuleIds.Finance,
        });
    }

    [Fact]
    public void HardDependencyClosure_Should_NotFollowSoftDependencies()
    {
        var closure = ModuleCatalog.HardDependencyClosure([ModuleIds.PersonalFinance]);

        closure.Should().BeEquivalentTo(new[] { ModuleIds.PersonalFinance },
            "personal-finance is deliberately soft on everything it reads");
    }

    [Fact]
    public void Dependents_Should_ReturnTransitiveHardDependents()
    {
        var dependents = ModuleCatalog.Dependents(ModuleIds.Finance);

        dependents.Should().BeEquivalentTo(new[] { ModuleIds.Commerce, ModuleIds.Subscriptions, ModuleIds.Workspaces });
        ModuleCatalog.Dependents(ModuleIds.Documents).Should().BeEmpty("nothing hard-depends on documents");
    }

    [Fact]
    public void ResolveEnabled_Should_ReturnEveryModule_When_ThereAreNoRows()
    {
        var enabled = ModuleCatalog.ResolveEnabled(new Dictionary<string, bool>());

        enabled.Should().BeEquivalentTo(AllIds, "a tenant with no rows resolves to exactly what is enabled today");
    }

    [Fact]
    public void ResolveEnabled_Should_TurnOffOnlyCommerce_When_CommerceRowIsOff()
    {
        var enabled = ModuleCatalog.ResolveEnabled(new Dictionary<string, bool>
        {
            [ModuleIds.Commerce] = false,
        });

        enabled.Should().NotContain(ModuleIds.Commerce);
        enabled.Should().BeEquivalentTo(AllIds.Except([ModuleIds.Commerce]));
    }

    [Fact]
    public void ResolveEnabled_Should_CloseOverHardDependencies_When_FinanceRowIsOff()
    {
        var enabled = ModuleCatalog.ResolveEnabled(new Dictionary<string, bool>
        {
            [ModuleIds.Finance] = false,
            // An explicit "on" for a dependent does not survive its dependency being off.
            [ModuleIds.Commerce] = true,
        });

        enabled.Should().NotContain(ModuleIds.Finance);
        enabled.Should().NotContain(ModuleIds.Commerce, "commerce hard-depends on finance");
        enabled.Should().NotContain(ModuleIds.Subscriptions, "subscriptions hard-depends on finance");
        enabled.Should().NotContain(ModuleIds.Workspaces, "workspaces hard-depends on subscriptions");
        enabled.Should().Contain(ModuleIds.PersonalFinance, "personal-finance is only soft on finance");
        enabled.Should().Contain(ModuleIds.Groups);
        enabled.Should().Contain(ModuleIds.Ordering);
    }

    [Fact]
    public void ResolveEnabled_Should_IgnoreAnExplicitOff_When_ModuleIsCore()
    {
        var enabled = ModuleCatalog.ResolveEnabled(new Dictionary<string, bool>
        {
            [ModuleIds.Platform] = false,
            [ModuleIds.Ordering] = false,
            [ModuleIds.Ai] = false,
            [ModuleIds.Agents] = false,
        });

        enabled.Should().Contain(ExpectedCore, "core modules are forced on regardless of rows");
        enabled.Should().Contain(ModuleIds.Finance, "ordering stayed on, so finance is unaffected");
    }

    [Fact]
    public void ResolveEnabled_Should_IgnoreRows_When_IdIsNotInTheCatalogue()
    {
        var enabled = ModuleCatalog.ResolveEnabled(new Dictionary<string, bool>
        {
            ["retired-module"] = true,
            ["another"] = false,
        });

        enabled.Should().BeEquivalentTo(AllIds);
    }

    [Fact]
    public void ResolveEnabled_Should_HonourExplicitOn_When_DefaultIsOff()
    {
        var descriptors = new List<ModuleDescriptor>
        {
            new("core", "Core", "", IsCore: true, DependsOn: [], SoftDependsOn: []),
            new("opt-in", "Opt in", "", IsCore: false, DependsOn: ["core"], SoftDependsOn: [], DefaultEnabled: false),
        };

        ModuleCatalog.ResolveEnabled(new Dictionary<string, bool>(), descriptors)
            .Should().BeEquivalentTo(new[] { "core" });
        ModuleCatalog.ResolveEnabled(new Dictionary<string, bool> { ["opt-in"] = true }, descriptors)
            .Should().BeEquivalentTo(new[] { "core", "opt-in" });
    }

    [Fact]
    public void Validate_Should_Reject_When_HardDependencyGraphHasACycle()
    {
        var descriptors = new List<ModuleDescriptor>
        {
            new("a", "A", "", IsCore: false, DependsOn: ["b"], SoftDependsOn: []),
            new("b", "B", "", IsCore: false, DependsOn: ["c"], SoftDependsOn: []),
            new("c", "C", "", IsCore: false, DependsOn: ["a"], SoftDependsOn: []),
        };

        var act = () => ModuleCatalog.Validate(descriptors);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cycle*");
    }

    [Fact]
    public void Validate_Should_Reject_When_ModuleDependsOnItself()
    {
        var descriptors = new List<ModuleDescriptor>
        {
            new("a", "A", "", IsCore: false, DependsOn: ["a"], SoftDependsOn: []),
        };

        var act = () => ModuleCatalog.Validate(descriptors);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cycle*");
    }

    [Fact]
    public void Validate_Should_Reject_When_DependencyIdIsUnknown()
    {
        var descriptors = new List<ModuleDescriptor>
        {
            new("a", "A", "", IsCore: false, DependsOn: ["missing"], SoftDependsOn: []),
        };

        var act = () => ModuleCatalog.Validate(descriptors);

        act.Should().Throw<InvalidOperationException>().WithMessage("*missing*");
    }

    [Fact]
    public void Validate_Should_Reject_When_SoftDependencyIdIsUnknown()
    {
        var descriptors = new List<ModuleDescriptor>
        {
            new("a", "A", "", IsCore: false, DependsOn: [], SoftDependsOn: ["missing"]),
        };

        var act = () => ModuleCatalog.Validate(descriptors);

        act.Should().Throw<InvalidOperationException>().WithMessage("*missing*");
    }

    [Fact]
    public void Validate_Should_Reject_When_CoreModuleHardDependsOnNonCore()
    {
        var descriptors = new List<ModuleDescriptor>
        {
            new("core", "Core", "", IsCore: true, DependsOn: ["optional"], SoftDependsOn: []),
            new("optional", "Optional", "", IsCore: false, DependsOn: [], SoftDependsOn: []),
        };

        var act = () => ModuleCatalog.Validate(descriptors);

        act.Should().Throw<InvalidOperationException>().WithMessage("*non-core*");
    }

    [Fact]
    public void Validate_Should_Reject_When_IdsAreDuplicated()
    {
        var descriptors = new List<ModuleDescriptor>
        {
            new("a", "A", "", IsCore: false, DependsOn: [], SoftDependsOn: []),
            new("a", "A again", "", IsCore: false, DependsOn: [], SoftDependsOn: []),
        };

        var act = () => ModuleCatalog.Validate(descriptors);

        act.Should().Throw<InvalidOperationException>().WithMessage("*more than once*");
    }

    [Fact]
    public void TryGetModuleId_Should_ReturnNull_When_AssemblyCarriesNoAttribute()
    {
        // SharedKernel itself is never gated and never declares a module.
        ModuleCatalog.TryGetModuleId(typeof(ModuleCatalog).Assembly).Should().BeNull();
        ModuleCatalog.TryGetModuleId(typeof(ModuleCatalog)).Should().BeNull();
    }

    [Fact]
    public void ModuleDependencyException_Should_ExposeCodeModuleAndRelatedIds()
    {
        var exception = new ModuleDependencyException(
            ModuleDependencyException.DependencyMissing, ModuleIds.Commerce, [ModuleIds.Finance]);

        exception.Code.Should().Be(ModuleErrorCodes.DependencyMissing);
        exception.ModuleId.Should().Be(ModuleIds.Commerce);
        exception.RelatedModuleIds.Should().BeEquivalentTo(new[] { ModuleIds.Finance });
        ModuleDependencyException.DependentsEnabled.Should().Be(ModuleErrorCodes.DependentsEnabled);
    }

    [Fact]
    public void ModuleDisabledException_Should_CarryTheDisabledCode()
    {
        var exception = new ModuleDisabledException(ModuleIds.Commerce);

        exception.ModuleId.Should().Be(ModuleIds.Commerce);
        exception.Code.Should().Be(ModuleErrorCodes.Disabled);
        ModuleErrorCodes.Disabled.Should().Be("module.disabled");
    }
}
