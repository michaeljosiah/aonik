using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>Spec 070 §5/§11 — facet definition authoring: SourcePath rules, option-token
/// discipline, range-band geometry, and the omitted-means-unchanged update contract.</summary>
public class FacetGroupServiceTests
{
    [Fact]
    public async Task Create_Should_RequireSourcePath_ForAttributeAndRange_AndForbidItOtherwise()
    {
        var facets = NewService();

        var rangeWithout = () => facets.CreateAsync(new CreateFacetGroupCommand(
            "calories", "Calories", FacetMatchKinds.Range, """[{"value":"a","label":"A","max":500}]"""));
        var tagWith = () => facets.CreateAsync(new CreateFacetGroupCommand(
            "dietary", "Dietary", FacetMatchKinds.Tag, """[{"value":"vegan","label":"Vegan"}]""", SourcePath: "tags"));

        (await rangeWithout.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("sourcePath");
        (await tagWith.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("must not carry");
    }

    [Fact]
    public async Task Create_Should_RejectMalformedOptionDefinitions()
    {
        var facets = NewService();

        var cases = new (string Options, string Because)[]
        {
            ("not json", "not JSON at all"),
            ("""{"value":"x"}""", "not an array"),
            ("[]", "empty"),
            ("""[{"label":"No value"}]""", "missing value token"),
            ("""[{"value":"a","label":"A"},{"value":"a","label":"Again"}]""", "duplicate value tokens"),
            ("""[{"value":"a","label":"A","min":5}]""", "bounds on a non-range group"),
        };

        foreach (var (options, because) in cases)
        {
            var act = () => facets.CreateAsync(new CreateFacetGroupCommand(
                $"g{Guid.NewGuid():N}"[..10], "Group", FacetMatchKinds.Tag, options));
            await act.Should().ThrowAsync<StorefrontValidationException>(because);
        }
    }

    [Fact]
    public async Task Create_Should_EnforceRangeBandGeometry()
    {
        var facets = NewService();

        Func<Task> Range(string options) => () => facets.CreateAsync(new CreateFacetGroupCommand(
            $"r{Guid.NewGuid():N}"[..10], "Range", FacetMatchKinds.Range, options, SourcePath: "kcal"));

        // min >= max is an empty band; overlap makes counts ambiguous; declaration order is render
        // order. Adjacent half-open bands sharing a boundary are legal and gapless.
        await Range("""[{"value":"a","label":"A","min":500,"max":500}]""")
            .Should().ThrowAsync<StorefrontValidationException>();
        await Range("""[{"value":"a","label":"A","max":600},{"value":"b","label":"B","min":500,"max":800}]""")
            .Should().ThrowAsync<StorefrontValidationException>();
        await Range("""[{"value":"b","label":"B","min":500,"max":800},{"value":"a","label":"A","max":500}]""")
            .Should().ThrowAsync<StorefrontValidationException>();
        await Range("""[{"value":"a","label":"A","max":500},{"value":"b","label":"B","min":500,"max":800}]""")
            .Should().NotThrowAsync("adjacent [_,500) + [500,_) bands are disjoint");
    }

    [Fact]
    public async Task Update_Should_PreserveOmittedMembers_AndRevalidateSuppliedOptions()
    {
        var facets = NewService();
        var created = await facets.CreateAsync(new CreateFacetGroupCommand(
            "spice", "Spice", FacetMatchKinds.Attribute,
            """[{"value":"mild","label":"Mild"}]""", SourcePath: "spice", SortOrder: 3));

        var renamed = await facets.UpdateAsync(created.Id, new UpdateFacetGroupCommand("Heat"));
        renamed.SortOrder.Should().Be(3);
        renamed.IsActive.Should().BeTrue();
        renamed.SourcePath.Should().Be("spice");
        renamed.Options.Should().ContainSingle(o => o.Value == "mild");

        var badOptions = () => facets.UpdateAsync(created.Id, new UpdateFacetGroupCommand("Heat", OptionsJson: "broken"));
        await badOptions.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task PublicList_Should_ServeActiveGroupsOnly_InOrder()
    {
        var facets = NewService();
        var a = await facets.CreateAsync(new CreateFacetGroupCommand(
            "dietary", "Dietary", FacetMatchKinds.Tag, """[{"value":"vegan","label":"Vegan"}]""", SortOrder: 2));
        await facets.CreateAsync(new CreateFacetGroupCommand(
            "spice", "Spice", FacetMatchKinds.Attribute, """[{"value":"mild","label":"Mild"}]""", SourcePath: "spice", SortOrder: 1));

        await facets.UpdateAsync(a.Id, new UpdateFacetGroupCommand("Dietary", IsActive: false));

        (await facets.ListPublicAsync()).Select(g => g.Key).Should().Equal("spice");
        (await facets.ListAdminAsync()).Select(g => g.Key).Should().Equal("spice", "dietary");
    }

    [Fact]
    public async Task Create_Should_RejectDuplicateKeys()
    {
        var facets = NewService();
        await facets.CreateAsync(new CreateFacetGroupCommand(
            "dietary", "Dietary", FacetMatchKinds.Tag, """[{"value":"vegan","label":"Vegan"}]"""));

        var duplicate = () => facets.CreateAsync(new CreateFacetGroupCommand(
            "dietary", "Again", FacetMatchKinds.Tag, """[{"value":"x","label":"X"}]"""));

        await duplicate.Should().ThrowAsync<StorefrontValidationException>();
    }

    private static FacetGroupService NewService()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        return new FacetGroupService(ctx, new TestTenantProvider(tenantId));
    }
}
