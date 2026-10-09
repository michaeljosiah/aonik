using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Inventory;
using Aonik.Database.Tests.Support;
using Aonik.Infrastructure.Multitenancy;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Database.Tests.Commerce;

/// <summary>
/// Spec 068 A17 on the only provider that can assert it: two adds racing into a box's last
/// remaining space touch DIFFERENT line rows, so nothing collides at the row level — the Cart
/// row's rowversion, touched by every capacity-affecting write, is what serializes them. The
/// InMemory provider enforces no rowversion, so its suite is structurally unable to fail here.
/// </summary>
public class BoxCartCapacitySqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public BoxCartCapacitySqlServerTests(SqlLocalDbFixture db) => _db = db;

    private sealed class WallClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed class NoSettings :
        Aonik.SharedKernel.Abstractions.Settings.ITenantSettingStore,
        Aonik.SharedKernel.Abstractions.Settings.ISettingProvider
    {
        public Task<string?> GetTenantValueAsync(string key, Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task SetTenantValueAsync(string key, string? value, Guid tenantId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<string?> GetAsync(string key, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task<string> GetRequiredAsync(string key, CancellationToken ct = default)
            => throw new InvalidOperationException();

        public Task<string?> GetForScopeAsync(
            string key, Aonik.SharedKernel.Abstractions.Settings.SettingScope scope,
            Guid? tenantId = null, Guid? userId = null, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        public Task<Aonik.SharedKernel.Abstractions.Settings.SettingResolution> GetResolvedAsync(
            string key, Guid? tenantId = null, Guid? userId = null, CancellationToken ct = default)
            => Task.FromResult(new Aonik.SharedKernel.Abstractions.Settings.SettingResolution(key, null, "none"));
    }

    private sealed class GbpCurrency : Aonik.SharedKernel.Abstractions.ITenantCurrencyProvider
    {
        public Task<List<string>> GetTenantCurrencyCodesAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(new List<string> { "GBP" });

        public Task<string?> GetTenantDefaultCurrencyAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<string?>("GBP");
    }

    private BoxCartService NewBoxCarts(CommerceDbContext context, Guid tenantId)
    {
        var options = CommerceSqlServerHarness.CreateOptionService(context, tenantId);
        var settings = new NoSettings();
        return new BoxCartService(
            context,
            new TestTenantProvider(tenantId),
            new OptionSelectionService(context, options, new TestTenantProvider(tenantId)),
            new InventoryService(context, new TestTenantProvider(tenantId),
                new TenantContext { TenantId = tenantId }, new WallClock()),
            settings,
            settings,
            new GbpCurrency(),
            new ProductPricingService(context, new TestTenantProvider(tenantId), new WallClock()), new WallClock(),
            DiscountQuoteTestFactory.Create(context, tenantId, new WallClock()));
    }

    [SkippableFact]
    public async Task TwoAddsRacingIntoTheLastSpace_Should_CommitExactlyOne()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
        var tenantId = Guid.NewGuid();
        var (bundleId, variantA, variantB) = await SeedAsync(tenantId);

        Guid cartId;
        string token;
        string version;
        await using (var setup = CommerceSqlServerHarness.CreateContext(_db, tenantId))
        {
            var carts = NewBoxCarts(setup, tenantId);
            var box = await carts.CreateAsync(new CreateBoxCartCommand(bundleId, 6));
            cartId = box.Box.CartId;
            token = box.CartToken!;
            var populated = await carts.AddLineAsync(cartId, new AddBoxLineCommand(variantA, 5, null),
                CartAccessContext.ForGuest(token, box.CartVersion));
            version = populated.CartVersion;
        }

        // Two writers over two independent contexts — different new line rows, one last space.
        await using var contextA = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        await using var contextB = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var access = CartAccessContext.ForGuest(token, version);
        var addA = Task.Run(() => NewBoxCarts(contextA, tenantId)
            .AddLineAsync(cartId, new AddBoxLineCommand(variantA, 1, null), access));
        var addB = Task.Run(() => NewBoxCarts(contextB, tenantId)
            .AddLineAsync(cartId, new AddBoxLineCommand(variantB, 1, null), access));

        var results = await Task.WhenAll(
            Capture(addA),
            Capture(addB));

        results.Count(r => r.Succeeded).Should().Be(1, "a seven-unit six-box is unrepresentable");
        var loser = results.Single(r => !r.Succeeded);
        (loser.Error is CartWriteConflictException or DbUpdateConcurrencyException).Should().BeTrue(
            "the stale mutation must fail its explicit precondition or native parent claim without being replayed");

        await using var verify = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var units = await verify.CartItems
            .Where(i => i.CartId == cartId)
            .SumAsync(i => i.Quantity);
        units.Should().Be(6m);
    }

    private static async Task<(bool Succeeded, Exception? Error)> Capture(Task task)
    {
        try
        {
            await task;
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }

    [SkippableFact]
    public async Task CatalogueRepair_Should_AdvanceTheParentVersion_WithoutRenewingActivity()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
        var tenantId = Guid.NewGuid();
        var (bundleId, variantId, _) = await SeedAsync(tenantId);
        await using var context = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var boxes = NewBoxCarts(context, tenantId);
        var created = await boxes.CreateAsync(new CreateBoxCartCommand(bundleId, 6));
        var populated = await boxes.AddLineAsync(created.Box.CartId, new AddBoxLineCommand(variantId, 1),
            CartAccessContext.ForGuest(created.CartToken, created.CartVersion));
        var originalActivity = (await context.Carts.AsNoTracking().SingleAsync()).LastActivityAtUtc;
        await using (var author = CommerceSqlServerHarness.CreateContext(_db, tenantId))
        {
            var variant = await author.ProductVariants.SingleAsync(row => row.Id == variantId);
            var product = await author.Products.SingleAsync(row => row.Id == variant.ProductId);
            product.UnitSurcharge = 2m;
            product.UnitSurchargeCurrency = "GBP";
            await author.SaveChangesAsync();
        }

        var repaired = await boxes.GetAsync(created.Box.CartId, CartAccessContext.ForGuest(created.CartToken));

        repaired.CartVersion.Should().NotBe(populated.CartVersion);
        repaired.Box.Lines.Single().UnitSurcharge.Should().Be(2m);
        (await context.Carts.AsNoTracking().SingleAsync()).LastActivityAtUtc.Should().Be(originalActivity);
        var unchanged = await boxes.GetAsync(created.Box.CartId, CartAccessContext.ForGuest(created.CartToken));
        unchanged.CartVersion.Should().Be(repaired.CartVersion, "an unchanged GET does not write or renew the draft");
        var tenant = new TestTenantProvider(tenantId);
        var stale = () => new CartService(context, tenant, new ProductPricingService(context, tenant, new WallClock()), new WallClock(),
                DiscountQuoteTestFactory.Create(context, tenantId, new WallClock()))
            .SaveCheckoutDraftAsync(created.Box.CartId, new CartCheckoutDraftDto(Notes: "Stale before repair"),
                CartAccessContext.ForGuest(created.CartToken, populated.CartVersion));
        await stale.Should().ThrowAsync<CartWriteConflictException>();
    }

    [SkippableFact]
    public async Task FailedBoxMutation_Should_NotLeakItsQuantityIntoALaterInventorySave()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
        var tenantId = Guid.NewGuid();
        var (bundleId, variantId, _) = await SeedAsync(tenantId);
        await using var context = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var boxes = NewBoxCarts(context, tenantId);
        var created = await boxes.CreateAsync(new CreateBoxCartCommand(bundleId, 6));
        var populated = await boxes.AddLineAsync(created.Box.CartId, new AddBoxLineCommand(variantId, 1),
            CartAccessContext.ForGuest(created.CartToken, created.CartVersion));
        using var personalisation = JsonDocument.Parse("{}");
        var act = () => boxes.UpdateLineAsync(created.Box.CartId, populated.Box.Lines.Single().LineId,
            new UpdateBoxLineCommand(2, personalisation.RootElement, ApplyToUnits: 3),
                CartAccessContext.ForGuest(created.CartToken, populated.CartVersion));

        await act.Should().ThrowAsync<StorefrontValidationException>().WithMessage("*applyToUnits*");
        await new InventoryService(context, new TestTenantProvider(tenantId),
            new TenantContext { TenantId = tenantId }, new WallClock()).SetOnHandAsync(variantId, 60m);

        await using var verify = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        (await verify.CartItems.SingleAsync()).Quantity.Should().Be(1m);
        Convert.ToBase64String((await verify.Carts.SingleAsync()).RowVersion).Should().Be(populated.CartVersion);
    }

    private async Task<(Guid BundleId, Guid VariantA, Guid VariantB)> SeedAsync(Guid tenantId)
    {
        await using var context = CommerceSqlServerHarness.CreateContext(_db, tenantId);

        var dishAId = Guid.NewGuid();
        var dishBId = Guid.NewGuid();
        var variantAId = Guid.NewGuid();
        var variantBId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();

        context.Products.AddRange(
            new Product { Id = dishAId, TenantId = tenantId, Slug = "dish-a", Name = "Dish A", Kind = ProductKinds.Simple, Status = ProductStatuses.Active },
            new Product { Id = dishBId, TenantId = tenantId, Slug = "dish-b", Name = "Dish B", Kind = ProductKinds.Simple, Status = ProductStatuses.Active },
            new Product
            {
                Id = bundleId, TenantId = tenantId, Slug = "meal-box", Name = "Meal Box", Kind = ProductKinds.Bundle,
                Status = ProductStatuses.Active, BundlePricingMode = BundlePricingModes.SizeTiered,
            });
        context.ProductVariants.AddRange(
            new ProductVariant { Id = variantAId, TenantId = tenantId, ProductId = dishAId, Sku = "A", Name = "Dish A" },
            new ProductVariant { Id = variantBId, TenantId = tenantId, ProductId = dishBId, Sku = "B", Name = "Dish B" });
        context.BundleSlots.Add(new BundleSlot
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BundleProductId = bundleId,
            Name = "Pick", MinItems = 0, MaxItems = 99, AllowDuplicates = true,
        });
        context.BundleSizePlans.Add(new BundleSizePlan
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BundleProductId = bundleId, MinSize = 6, MaxSize = 30,
            BaseSize = 6, BasePrice = 95m, PerSpacePrice = 15m, Currency = "GBP",
        });
        context.InventoryLevels.AddRange(
            new InventoryLevel { Id = Guid.NewGuid(), TenantId = tenantId, ProductVariantId = variantAId, StockItemKind = StockItemKinds.ProductVariant, OnHand = 50m },
            new InventoryLevel { Id = Guid.NewGuid(), TenantId = tenantId, ProductVariantId = variantBId, StockItemKind = StockItemKinds.ProductVariant, OnHand = 50m });
        await context.SaveChangesAsync();

        return (bundleId, variantAId, variantBId);
    }
}
