using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Database.Tests.Support;
using Aonik.IntegrationTests.Support;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Database.Tests.Commerce;

/// <summary>
/// The Spec 066 §5 write-write contention design, asserted on the only provider
/// that can assert it. <c>AonikDbContextBase.ConfigureRowVersions</c> maps
/// <c>RowVersion</c> to the engine-generated <c>rowversion</c> type, so on SQL
/// Server every UPDATE bumps the token and a stale writer loses with
/// <see cref="DbUpdateConcurrencyException"/>; on InMemory the token never
/// changes and both writers would silently commit.
///
/// <c>SetProductOptionGroupsAsync</c> and <c>SetRecommendedDefaultAsync</c> each
/// validate against state the other is about to change, and serialise via
/// <c>TouchGroups</c> — deliberately marking the shared option-group row
/// modified so the two writes contend on its rowversion. These tests stage the
/// classic stale-read interleaving deterministically: the losing service's
/// context reads the group first (a tracked read, as its own validation queries
/// do), the winning service commits through a second context, and the loser's
/// SaveChanges must then conflict rather than commit a disagreement.
/// </summary>
public class OptionGroupConcurrencySqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public OptionGroupConcurrencySqlServerTests(SqlLocalDbFixture db)
    {
        _db = db;
    }

    [SkippableFact]
    public async Task SaveChanges_Should_ThrowConcurrencyException_When_OptionGroupRowVersionIsStale()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var (groupId, _) = await CommerceSqlServerHarness.SeedPortionGroupAndProductAsync(_db, tenantId);

        await using var first = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        await using var second = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var groupInFirst = await first.OptionGroups.SingleAsync(g => g.Id == groupId);
        var groupInSecond = await second.OptionGroups.SingleAsync(g => g.Id == groupId);

        groupInFirst.Label = "First writer";
        await first.SaveChangesAsync();

        groupInSecond.Label = "Second writer";
        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the engine bumped the rowversion under the second writer");
    }

    [SkippableFact]
    public async Task SetProductOptionGroups_Should_LoseOnGroupRowVersion_When_DefaultMovedConcurrently()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var (groupId, productId) = await CommerceSqlServerHarness.SeedPortionGroupAndProductAsync(_db, tenantId);

        // The narrowing service reads the group (tracked) before the competing
        // writer commits — the stale-validation window Spec 066 contends on.
        await using var staleContext = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var staleService = CommerceSqlServerHarness.CreateOptionService(staleContext, tenantId);
        _ = await staleContext.OptionGroups.Include(g => g.Choices).SingleAsync(g => g.Id == groupId);

        await using (var freshContext = CommerceSqlServerHarness.CreateContext(_db, tenantId))
        {
            var freshService = CommerceSqlServerHarness.CreateOptionService(freshContext, tenantId);
            await freshService.SetRecommendedDefaultAsync(groupId, "full");
        }

        var act = () => staleService.SetProductOptionGroupsAsync(
            productId,
            new SetProductOptionGroupsCommand([new ProductOptionGroupLine("portion")]));

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the narrowing validated against the pre-move default and must not commit over it");

        // The loser committed nothing: the product still has no narrowing rows.
        await using var verify = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        (await verify.ProductOptionGroups.AnyAsync(x => x.ProductId == productId)).Should().BeFalse();
    }

    // ── The default-move side of the race ──────────────────────────────────
    //
    // PR #258 changed the default move's contract from mechanism to outcome: the
    // execution-strategy delegate RELOADS the group and its choices at the start
    // of every attempt and re-runs validation against that snapshot, so a
    // narrowing that committed BEFORE the attempt is SEEN — absorbed when
    // compatible, V11-rejected when it would orphan a product — rather than
    // producing a blind token conflict the operator must retry into the same
    // answer. Only a write landing between the reload and the save trips the
    // rowversion (not stageable without fault injection; the two proven halves
    // compose). These tests assert the two visible halves on the real provider.

    [SkippableFact]
    public async Task SetRecommendedDefault_Should_RevalidateAndSucceed_When_ACompatibleNarrowingCommittedFirst()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var (groupId, productId) = await CommerceSqlServerHarness.SeedPortionGroupAndProductAsync(_db, tenantId);

        // The move's context reads the group before the competing writer commits —
        // the classic stale window. The narrowing pins the product's OWN default
        // (light), so a group-default move to full orphans nobody.
        await using var staleContext = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var staleService = CommerceSqlServerHarness.CreateOptionService(staleContext, tenantId);
        _ = await staleContext.OptionGroups.Include(g => g.Choices).SingleAsync(g => g.Id == groupId);

        await using (var freshContext = CommerceSqlServerHarness.CreateContext(_db, tenantId))
        {
            var freshService = CommerceSqlServerHarness.CreateOptionService(freshContext, tenantId);
            await freshService.SetProductOptionGroupsAsync(
                productId,
                new SetProductOptionGroupsCommand(
                    [new ProductOptionGroupLine("portion", AllowedChoiceKeys: ["light"], DefaultChoiceKey: "light")]));
        }

        var result = await staleService.SetRecommendedDefaultAsync(groupId, "full");

        // The reload made the pre-attempt narrowing visible; revalidation passed
        // (the product keeps its pinned light), and the move committed cleanly.
        result.Group.Choices.Single(c => c.Key == "full").IsRecommendedDefault.Should().BeTrue();
        result.AffectedProductSlugs.Should().BeEmpty("the product inherits nothing — it pinned its own default");

        await using var verify = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var choices = await verify.OptionChoices.Where(c => c.OptionGroupId == groupId).ToListAsync();
        choices.Single(c => c.Key == "full").IsRecommendedDefault.Should().BeTrue();
        choices.Single(c => c.Key == "light").IsRecommendedDefault.Should().BeFalse();
        (await verify.ProductOptionGroups.SingleAsync(x => x.ProductId == productId))
            .DefaultChoiceKey.Should().Be("light", "the narrowing's own default is untouched");
    }

    [SkippableFact]
    public async Task SetRecommendedDefault_Should_RejectViaRevalidation_When_TheNarrowingWouldOrphanTheProduct()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var (groupId, productId) = await CommerceSqlServerHarness.SeedPortionGroupAndProductAsync(_db, tenantId);

        await using var staleContext = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var staleService = CommerceSqlServerHarness.CreateOptionService(staleContext, tenantId);
        _ = await staleContext.OptionGroups.Include(g => g.Choices).SingleAsync(g => g.Id == groupId);

        // No override this time: the product narrows to {light} and INHERITS the
        // group default. Moving that default to full would leave it unresolvable —
        // exactly what V11 exists to prevent, and what the pre-#258 token conflict
        // only prevented by making the operator retry into this same rejection.
        await using (var freshContext = CommerceSqlServerHarness.CreateContext(_db, tenantId))
        {
            var freshService = CommerceSqlServerHarness.CreateOptionService(freshContext, tenantId);
            await freshService.SetProductOptionGroupsAsync(
                productId,
                new SetProductOptionGroupsCommand(
                    [new ProductOptionGroupLine("portion", AllowedChoiceKeys: ["light"])]));
        }

        var act = () => staleService.SetRecommendedDefaultAsync(groupId, "full");

        (await act.Should().ThrowAsync<Aonik.Commerce.Services.Catalog.OptionValidationException>(
                "revalidation inside the attempt sees the committed narrowing"))
            .Which.RuleId.Should().Be("V11");

        // Rejected before any write staged: the old default is intact and the
        // group is still servable — no half-demoted state exists to roll back.
        await using var verify = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var choices = await verify.OptionChoices.Where(c => c.OptionGroupId == groupId).ToListAsync();
        choices.Single(c => c.Key == "light").IsRecommendedDefault.Should().BeTrue();
        choices.Single(c => c.Key == "full").IsRecommendedDefault.Should().BeFalse();
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
