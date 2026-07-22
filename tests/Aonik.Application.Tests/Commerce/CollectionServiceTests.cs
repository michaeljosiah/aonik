using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Spec 070 §5/§10/§11 — curated collections: authoring invariants, full-replace reorder
/// semantics, the soft-delete revive path, and the public/admin visibility split.
/// Covers acceptance criteria A1 (service half), A9, A12.
/// </summary>
public class CollectionServiceTests
{
    [Fact]
    public async Task ReplaceItems_Should_ReturnMembersInRankOrder_AndReorderOnReplace()
    {
        // A1 — reordering in admin changes the next read, no release.
        var (builder, _) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();
        var collectionId = await builder.WithCollectionAsync("featured", ("jollof", 2), ("egusi", 1));

        (await builder.Collections.GetPublicBySlugAsync("featured"))!
            .Products.Select(p => p.Slug).Should().ContainInOrder("egusi", "jollof");

        await builder.Collections.ReplaceItemsAsync(collectionId, new ReplaceCollectionItemsCommand(
            [new CollectionItemLine(ids["jollof"], 1), new CollectionItemLine(ids["egusi"], 2)]));

        (await builder.Collections.GetPublicBySlugAsync("featured"))!
            .Products.Select(p => p.Slug).Should().ContainInOrder("jollof", "egusi");
    }

    [Fact]
    public async Task ReplaceItems_Should_SwapRanks_AndRejectNegativeOnes()
    {
        // The swap is the P1 shape: A:1,B:2 → A:2,B:1. InMemory proves the semantics; the
        // per-statement index safety is proven on LocalDB (two-phase negative-temp staging), and
        // negative request ranks are rejected because phase 1 owns the negative space.
        var (builder, _) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();
        var collectionId = await builder.WithCollectionAsync("featured", ("jollof", 1), ("egusi", 2));

        var swapped = await builder.Collections.ReplaceItemsAsync(collectionId, new ReplaceCollectionItemsCommand(
            [new CollectionItemLine(ids["jollof"], 2), new CollectionItemLine(ids["egusi"], 1)]));
        swapped.Items.OrderBy(i => i.Rank).Select(i => i.Slug).Should().ContainInOrder("egusi", "jollof");

        var negative = () => builder.Collections.ReplaceItemsAsync(collectionId, new ReplaceCollectionItemsCommand(
            [new CollectionItemLine(ids["jollof"], -1)]));
        await negative.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task Update_Should_PreserveTheSubtitle_UnlessExplicitlyCleared()
    {
        // A nullable string cannot carry both "unchanged" and "remove"; omission preserves,
        // ClearSubtitle removes — the same tri-state rule as ClearParent and ClearCategory.
        var (builder, _) = await ArrangeAsync();
        var created = await builder.Collections.CreateAsync(new CreateCollectionCommand(
            "featured", "Featured", Subtitle: "Chef's picks"));

        var renamed = await builder.Collections.UpdateAsync(created.Id, new UpdateCollectionCommand("Renamed"));
        renamed.Subtitle.Should().Be("Chef's picks", "an omitted subtitle must not erase the stored one");

        var cleared = await builder.Collections.UpdateAsync(created.Id, new UpdateCollectionCommand("Renamed", ClearSubtitle: true));
        cleared.Subtitle.Should().BeNull();
    }

    [Fact]
    public async Task ReplaceItems_Should_Reject_DuplicateRanksAndDuplicateProducts()
    {
        // A12 — ties would make curated order nondeterministic.
        var (builder, _) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();
        var collectionId = await builder.WithCollectionAsync();

        var duplicateRank = () => builder.Collections.ReplaceItemsAsync(collectionId, new ReplaceCollectionItemsCommand(
            [new CollectionItemLine(ids["jollof"], 1), new CollectionItemLine(ids["egusi"], 1)]));
        var duplicateProduct = () => builder.Collections.ReplaceItemsAsync(collectionId, new ReplaceCollectionItemsCommand(
            [new CollectionItemLine(ids["jollof"], 1), new CollectionItemLine(ids["jollof"], 2)]));

        (await duplicateRank.Should().ThrowAsync<StorefrontValidationException>()).Which.Message.Should().Contain("Rank");
        await duplicateProduct.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task ReplaceItems_Should_Reject_NullItems_ButAllowAnExplicitEmptyClear()
    {
        // The G1 lesson, applied here from day one: a missing property must never read as a clear.
        var (builder, _) = await ArrangeAsync();
        var collectionId = await builder.WithCollectionAsync("featured", ("jollof", 1));

        var nullItems = () => builder.Collections.ReplaceItemsAsync(collectionId, new ReplaceCollectionItemsCommand(null));
        await nullItems.Should().ThrowAsync<StorefrontValidationException>();

        var cleared = await builder.Collections.ReplaceItemsAsync(collectionId, new ReplaceCollectionItemsCommand([]));
        cleared.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceItems_Should_ReviveASoftDeletedMembership_When_AProductIsReAdded()
    {
        // §5 — the unique indexes filter IsDeleted, and re-adding must revive the soft-deleted row
        // rather than insert a duplicate that would collide with it on SQL Server.
        var (builder, ctx) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();
        var collectionId = await builder.WithCollectionAsync("featured", ("jollof", 1), ("egusi", 2));

        // Remove jollof, then re-add it at a new rank.
        await builder.Collections.ReplaceItemsAsync(collectionId, new ReplaceCollectionItemsCommand(
            [new CollectionItemLine(ids["egusi"], 1)]));
        await builder.Collections.ReplaceItemsAsync(collectionId, new ReplaceCollectionItemsCommand(
            [new CollectionItemLine(ids["egusi"], 1), new CollectionItemLine(ids["jollof"], 2)]));

        (await builder.Collections.GetPublicBySlugAsync("featured"))!
            .Products.Select(p => p.Slug).Should().ContainInOrder("egusi", "jollof");

        // Exactly ONE PHYSICAL row per (collection, product) ever exists — revived, not
        // duplicated. Counted through IncludeSoftDeleted on purpose: the default filter hides
        // soft-deleted rows, which is precisely how a duplicate-inserting implementation would
        // sneak past this assertion while colliding with the filtered unique index on SQL Server.
        ctx.ChangeTracker.Clear();
        var rows = ctx.CollectionItems
            .IncludeSoftDeleted()
            .Where(i => i.CollectionId == collectionId && i.ProductId == ids["jollof"])
            .ToList();
        rows.Should().ContainSingle().Which.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task PublicReads_Should_HideDraftMembers_AndInactiveCollections()
    {
        // A9 — a draft product staged into a collection is admin-visible, publicly absent, and
        // surfaces the moment the product activates without touching the collection.
        var (builder, _) = await ArrangeAsync();
        var ids = await builder.ProductIdsBySlugAsync();
        var collectionId = await builder.WithCollectionAsync("featured", ("jollof", 1), ("secret-dish", 2));

        (await builder.Collections.GetPublicBySlugAsync("featured"))!
            .Products.Select(p => p.Slug).Should().BeEquivalentTo(["jollof"]);
        (await builder.Collections.GetAdminAsync(collectionId))
            .Items.Select(i => i.Slug).Should().BeEquivalentTo(["jollof", "secret-dish"]);

        await builder.Products.UpdateProductAsync(ids["secret-dish"], new UpdateProductCommand(Status: ProductStatuses.Active));
        (await builder.Collections.GetPublicBySlugAsync("featured"))!
            .Products.Select(p => p.Slug).Should().BeEquivalentTo(["jollof", "secret-dish"]);

        // Deactivating the collection removes it from every public read while admin still sees it.
        await builder.Collections.UpdateAsync(collectionId, new UpdateCollectionCommand("Featured", IsActive: false));
        (await builder.Collections.GetPublicBySlugAsync("featured")).Should().BeNull();
        (await builder.Collections.ListPublicAsync()).Should().BeEmpty();
        (await builder.Collections.ListAdminAsync()).Should().ContainSingle(c => c.Slug == "featured");
    }

    [Fact]
    public async Task ListPublic_Should_ValidateTheKindFilter_CaseInsensitively()
    {
        // §10 — "featured" must match Featured (an exact compare silently returns nothing under
        // case-sensitive collations), and an unknown kind is loud.
        var (builder, _) = await ArrangeAsync();
        await builder.WithCollectionAsync("featured", ("jollof", 1));

        (await builder.Collections.ListPublicAsync("featured")).Should().ContainSingle();
        (await builder.Collections.ListPublicAsync("CURATED")).Should().BeEmpty();

        var unknown = () => builder.Collections.ListPublicAsync("seasonal");
        await unknown.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task CreateAndUpdate_Should_EnforceSlugRules_AndPreserveOmittedMembers()
    {
        var (builder, _) = await ArrangeAsync();
        var collectionId = await builder.WithCollectionAsync();

        var duplicate = () => builder.Collections.CreateAsync(new CreateCollectionCommand("featured", "Again"));
        var badSlug = () => builder.Collections.CreateAsync(new CreateCollectionCommand("Not A Slug!", "X"));
        await duplicate.Should().ThrowAsync<StorefrontValidationException>();
        await badSlug.Should().ThrowAsync<StorefrontValidationException>();

        // A rename must not re-kind, reorder or deactivate — omitted means unchanged.
        await builder.Collections.UpdateAsync(collectionId, new UpdateCollectionCommand("Featured", Kind: CollectionKinds.Custom, SortOrder: 5));
        var renamed = await builder.Collections.UpdateAsync(collectionId, new UpdateCollectionCommand("Renamed"));

        renamed.Kind.Should().Be(CollectionKinds.Custom);
        renamed.SortOrder.Should().Be(5);
        renamed.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ReplaceItems_Should_Reject_ProductsOutsideTheTenant()
    {
        var (builder, _) = await ArrangeAsync();
        var collectionId = await builder.WithCollectionAsync();

        var act = () => builder.Collections.ReplaceItemsAsync(collectionId, new ReplaceCollectionItemsCommand(
            [new CollectionItemLine(Guid.NewGuid(), 1)]));

        await act.Should().ThrowAsync<StorefrontValidationException>();
    }

    [Fact]
    public async Task AdminOperations_Should_ThrowNotFound_ForUnknownCollections()
    {
        var (builder, _) = await ArrangeAsync();
        var missing = Guid.NewGuid();

        await FluentActions.Awaiting(() => builder.Collections.GetAdminAsync(missing))
            .Should().ThrowAsync<NotFoundException>();
        await FluentActions.Awaiting(() => builder.Collections.UpdateAsync(missing, new UpdateCollectionCommand("X")))
            .Should().ThrowAsync<NotFoundException>();
        await FluentActions.Awaiting(() => builder.Collections.ReplaceItemsAsync(missing, new ReplaceCollectionItemsCommand([])))
            .Should().ThrowAsync<NotFoundException>();
    }

    private static async Task<(MerchandisingBuilder Builder, Aonik.Commerce.Persistence.CommerceDbContext Ctx)> ArrangeAsync()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        var builder = new MerchandisingBuilder(ctx, tenantId);
        await builder.WithCategoriesAsync();
        await builder.WithProductsAsync();
        return (builder, ctx);
    }
}
