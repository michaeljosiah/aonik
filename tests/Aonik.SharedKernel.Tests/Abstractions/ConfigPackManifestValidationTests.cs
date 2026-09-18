using Aonik.SharedKernel.Abstractions.Packs;
using Aonik.SharedKernel.Modules;

using FluentAssertions;

namespace Aonik.SharedKernel.Tests.Abstractions;

/// <summary>
/// Spec 097 §13: a config pack's <c>modules</c> list is made of canonical catalogue ids, validated on
/// load. The three shipped packs must load with the revised lists, and an unknown (or legacy
/// PascalCase) id must fail the pack loudly, naming the pack and the id.
/// </summary>
public class ConfigPackManifestValidationTests
{
    private readonly ConfigPackSource _source = new();

    [Fact]
    public void Get_Should_LoadBasePack_When_ItDeclaresNoModules()
    {
        var pack = _source.Get("base");

        pack.Should().NotBeNull();
        pack!.Modules.Should().BeEmpty("the base pack leaves the catalogue defaults so a generic tenant gets everything");
    }

    [Fact]
    public void Get_Should_LoadSimiPack_WithCanonicalModuleIds()
    {
        var pack = _source.Get("simi");

        pack.Should().NotBeNull();
        pack!.Modules.Should().BeEquivalentTo(new[]
        {
            ModuleIds.PersonalFinance,
            ModuleIds.Groups,
            ModuleIds.Documents,
            ModuleIds.Ai,
            ModuleIds.Agents,
            ModuleIds.Voice,
        });
    }

    [Fact]
    public void Get_Should_LoadFoodCommercePack_WithCanonicalModuleIds()
    {
        var pack = _source.Get("food-commerce");

        pack.Should().NotBeNull();
        pack!.Modules.Should().BeEquivalentTo(new[] { ModuleIds.Commerce, ModuleIds.Ai, ModuleIds.Agents },
            "finance and ordering are implied through the hard-dependency closure, not declared");
        pack.Settings.Keys.Should().NotContain("Commerce.Enabled",
            "module enablement is a TenantModule row now, not a tenant setting");
        pack.Settings.Should().ContainKey("Branding.AgentDisplayName");
    }

    [Fact]
    public void ListBusinessTypes_Should_LoadEveryShippedPack_When_EachIsValidated()
    {
        var types = _source.ListBusinessTypes();

        types.Should().Contain(new[] { "base", "food-commerce", "simi" });

        foreach (var type in types)
        {
            var pack = _source.Get(type);
            pack.Should().NotBeNull($"'{type}' is listed so its resource must load");
            pack!.Modules.Should().OnlyContain(id => ModuleCatalog.IsKnown(id), $"'{type}' must only declare catalogue ids");
        }
    }

    [Fact]
    public void Validate_Should_Throw_When_ModuleIdIsUnknown()
    {
        // Legacy PascalCase spelling: comparison is case-sensitive on canonical ids, so this fails.
        var manifest = new ConfigPackManifest { BusinessType = "test-pack", Modules = ["Commerce"] };

        var act = () => ConfigPackSource.Validate(manifest, "test-pack.pack.json");

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("test-pack.pack.json").And.Contain("'Commerce'");
    }

    [Fact]
    public void Validate_Should_Throw_When_ModuleIdIsBlank()
    {
        var manifest = new ConfigPackManifest { BusinessType = "test-pack", Modules = [ModuleIds.Commerce, " "] };

        var act = () => ConfigPackSource.Validate(manifest, "test-pack.pack.json");

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("test-pack.pack.json").And.Contain("blank");
    }

    [Fact]
    public void Validate_Should_Pass_When_EveryIdIsCanonical()
    {
        var manifest = new ConfigPackManifest
        {
            BusinessType = "test-pack",
            Modules = ModuleCatalog.All.Select(descriptor => descriptor.Id).ToList(),
        };

        var act = () => ConfigPackSource.Validate(manifest, "test-pack.pack.json");

        act.Should().NotThrow();
    }
}
