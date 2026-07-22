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

    [SkippableFact]
    public async Task SetRecommendedDefault_Should_LoseAndRollBackItsDemote_When_NarrowingCommittedConcurrently()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var (groupId, productId) = await CommerceSqlServerHarness.SeedPortionGroupAndProductAsync(_db, tenantId);

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

        var act = () => staleService.SetRecommendedDefaultAsync(groupId, "full");

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the default move validated against the pre-narrowing state and must not commit over it");

        // The demote runs in its own round trip BEFORE the conflicting group
        // touch commits — only a real transaction rolls it back. This is the
        // assertion InMemory structurally cannot make: there, the demote would
        // stick and the group would be left with zero defaults.
        await using var verify = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var choices = await verify.OptionChoices.Where(c => c.OptionGroupId == groupId).ToListAsync();
        choices.Single(c => c.Key == "light").IsRecommendedDefault.Should().BeTrue("the failed move must leave the old default in place");
        choices.Single(c => c.Key == "full").IsRecommendedDefault.Should().BeFalse();
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
