using Aonik.SharedKernel.Abstractions.Packs;
using Aonik.SharedKernel.Modules;

using FluentAssertions;

namespace Aonik.SharedKernel.Tests.Abstractions;

/// <summary>
/// Spec 097 §13: a config pack's <c>modules</c> list is made of canonical catalogue ids, validated on
/// load. The shipped packs must load with canonical lists, and an unknown (or legacy
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
    public void Get_Should_LoadRemittancePack_WithoutSampleDataOrPinnedAgents()
    {
        // Arrange / Act
        var pack = _source.Get(" REMITTANCE ");

        // Assert
        pack.Should().NotBeNull();
        pack!.BusinessType.Should().Be("remittance");
        pack.DisplayName.Should().Be("Remittance");
        pack.Version.Should().Be(1);
        pack.Modules.Should().BeEquivalentTo(new[] { ModuleIds.Finance, ModuleIds.Ai, ModuleIds.Agents, ModuleIds.Documents });
        pack.Settings.Should().Contain("Branding.AgentDisplayName", "Remittance");
        pack.Agents.Should().BeEmpty("the pack must not invent a Remittance domain agent or pin a model");
        pack.ReferenceData.Should().BeEmpty("live corridors, partners and rates require operator configuration");
    }

    [Fact]
    public void Get_Should_LoadArkeWorldsPack_WithoutPublishingDraftOffersOrOverridingAgents()
    {
        // Arrange / Act
        var pack = _source.Get(" ARKE-WORLDS ");

        // Assert
        pack.Should().NotBeNull();
        pack!.BusinessType.Should().Be("arke-worlds");
        pack.DisplayName.Should().Be("Arke Worlds");
        pack.Version.Should().Be(2);
        pack.Modules.Should().BeEquivalentTo(new[] { ModuleIds.Workspaces, ModuleIds.Groups,
            ModuleIds.Subscriptions, ModuleIds.Finance, ModuleIds.Documents, ModuleIds.Ai, ModuleIds.Agents, ModuleIds.Publishing });
        pack.Settings.Should().ContainSingle().Which.Key.Should().Be("Branding.AgentDisplayName");
        pack.Settings["Branding.AgentDisplayName"].Should().Be("AONIK");
        pack.ReferenceData.Should().BeEmpty("commercial proposals must not become live prices or entitlements");
        pack.Agents.Should().BeEmpty("model routing remains authoritative");
    }

    [Fact]
    public void ListBusinessTypes_Should_LoadEveryShippedPack_When_EachIsValidated()
    {
        var types = _source.ListBusinessTypes();

        types.Should().Contain(new[] { "base", "food-commerce", "simi", "remittance", "arke-worlds" });

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
