using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Sourcing;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Supplier master + supplier catalog (Spec 053 §9): supplier CRUD with the per-tenant
/// duplicate-name guard, catalog upsert keyed by (supplier, ingredient) with the buy-side pack
/// economics, and the guards (active supplier/ingredient, positive pack size/price). The DB
/// unique indexes are the SQL Server backstop; InMemory does not enforce them, so the service
/// pre-checks are what these tests prove.
/// </summary>
public class SupplierServiceTests
{
    private static (SupplierService Service, CommerceDbContext Ctx, Guid TenantId) Build()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var ctx = CommerceTestHarness.CreateContext(options, tenantId);
        return (new SupplierService(ctx, new TestTenantProvider(tenantId)), ctx, tenantId);
    }

    private static async Task<Guid> SeedIngredientAsync(CommerceDbContext ctx, Guid tenantId, string name = "Rice", bool isActive = true)
    {
        var id = Guid.NewGuid();
        ctx.Ingredients.Add(new Ingredient
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            BaseUnit = IngredientBaseUnits.Kg,
            IsActive = isActive,
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Create_Should_CreateActiveSupplier_AndNormalizeCurrency()
    {
        var (service, ctx, _) = Build();
        await using var _ = ctx;

        var supplier = await service.CreateAsync(new CreateSupplierCommand(
            "  Mama Nkechi Farms  ", "ngn", PartyId: null, LeadTimeDays: 3, PaymentTerms: "Net 30"));

        supplier.Name.Should().Be("Mama Nkechi Farms");
        supplier.Currency.Should().Be("NGN");
        supplier.IsActive.Should().BeTrue();
        supplier.LeadTimeDays.Should().Be(3);
        supplier.PaymentTerms.Should().Be("Net 30");
        supplier.PartyId.Should().BeNull();
    }

    [Fact]
    public async Task Create_Should_Reject_DuplicateName()
    {
        var (service, ctx, _) = Build();
        await using var _ = ctx;

        await service.CreateAsync(new CreateSupplierCommand("Mama Nkechi Farms", "NGN"));

        var act = async () => await service.CreateAsync(new CreateSupplierCommand("  Mama Nkechi Farms ", "NGN"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Update_Should_Reject_NameCollidingWithAnotherSupplier()
    {
        var (service, ctx, _) = Build();
        await using var _ = ctx;

        await service.CreateAsync(new CreateSupplierCommand("Farm A", "NGN"));
        var b = await service.CreateAsync(new CreateSupplierCommand("Farm B", "NGN"));

        var act = async () => await service.UpdateAsync(new UpdateSupplierCommand(b.Id, "Farm A", "NGN"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Update_Should_PreserveIsActive_WhenNull_AndDeactivate_WhenFalse()
    {
        var (service, ctx, _) = Build();
        await using var _ = ctx;

        var created = await service.CreateAsync(new CreateSupplierCommand("Farm A", "NGN"));

        // Saying nothing about the flag must not change it.
        var untouched = await service.UpdateAsync(new UpdateSupplierCommand(created.Id, "Farm A", "NGN", IsActive: null));
        untouched.IsActive.Should().BeTrue();

        var deactivated = await service.UpdateAsync(new UpdateSupplierCommand(created.Id, "Farm A", "NGN", IsActive: false));
        deactivated.IsActive.Should().BeFalse();

        (await service.ListAsync()).Should().BeEmpty();
        (await service.ListAsync(includeInactive: true)).Should().ContainSingle(s => s.Id == created.Id);
    }

    [Fact]
    public async Task Get_Should_ReturnNull_WhenNotFound()
    {
        var (service, ctx, _) = Build();
        await using var _ = ctx;

        (await service.GetAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task Create_And_Update_Should_NormalizeEmptyPartyIdToNull()
    {
        var (service, ctx, _) = Build();
        await using var _ = ctx;

        // Guid.Empty means "not party-linked", never a real Party. Stored as-is it would make the
        // PO create emit an empty-party Supplier role — which the spine rejects — poisoning every
        // PO for this supplier; both write paths normalize it to null (the blank-SKU precedent).
        var created = await service.CreateAsync(new CreateSupplierCommand("Farm A", "NGN", PartyId: Guid.Empty));
        created.PartyId.Should().BeNull();
        (await ctx.Suppliers.SingleAsync(s => s.Id == created.Id)).PartyId.Should().BeNull();

        var realPartyId = Guid.NewGuid();
        var linked = await service.UpdateAsync(new UpdateSupplierCommand(created.Id, "Farm A", "NGN", PartyId: realPartyId));
        linked.PartyId.Should().Be(realPartyId); // a real link still round-trips

        var unlinked = await service.UpdateAsync(new UpdateSupplierCommand(created.Id, "Farm A", "NGN", PartyId: Guid.Empty));
        unlinked.PartyId.Should().BeNull();
        (await ctx.Suppliers.SingleAsync(s => s.Id == created.Id)).PartyId.Should().BeNull();
    }

    [Fact]
    public async Task UpsertCatalogItem_Should_Create_ThenUpdateTheSameRow()
    {
        var (service, ctx, tenantId) = Build();
        await using var _ = ctx;
        var rice = await SeedIngredientAsync(ctx, tenantId);
        var supplier = await service.CreateAsync(new CreateSupplierCommand("Farm A", "NGN"));

        var created = await service.UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(
            supplier.Id, rice, PackSize: 25m, PackPrice: 25_000m, Sku: "SUP-RICE-25"));

        created.PackSize.Should().Be(25m);
        created.PackPrice.Should().Be(25_000m);
        created.UnitPrice.Should().Be(1_000m); // PackPrice / PackSize, per base unit
        created.Currency.Should().Be("NGN");   // defaulted from the supplier
        created.IngredientName.Should().Be("Rice");
        created.IngredientBaseUnit.Should().Be(IngredientBaseUnits.Kg);

        // Upsert again for the same (supplier, ingredient): the row is updated, never duplicated.
        var updated = await service.UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(
            supplier.Id, rice, PackSize: 50m, PackPrice: 45_000m, Sku: "SUP-RICE-50"));

        updated.Id.Should().Be(created.Id);
        updated.PackSize.Should().Be(50m);
        updated.UnitPrice.Should().Be(900m);
        (await ctx.SupplierIngredients.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(0, 25_000)]
    [InlineData(-1, 25_000)]
    [InlineData(25, 0)]
    [InlineData(25, -5)]
    public async Task UpsertCatalogItem_Should_Reject_NonPositivePackSizeOrPrice(decimal packSize, decimal packPrice)
    {
        var (service, ctx, tenantId) = Build();
        await using var _ = ctx;
        var rice = await SeedIngredientAsync(ctx, tenantId);
        var supplier = await service.CreateAsync(new CreateSupplierCommand("Farm A", "NGN"));

        var act = async () => await service.UpsertCatalogItemAsync(
            new UpsertSupplierIngredientCommand(supplier.Id, rice, packSize, packPrice));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertCatalogItem_Should_Reject_WhenIngredientInactiveOrMissing()
    {
        var (service, ctx, tenantId) = Build();
        await using var _ = ctx;
        var stale = await SeedIngredientAsync(ctx, tenantId, "Old Spice Mix", isActive: false);
        var supplier = await service.CreateAsync(new CreateSupplierCommand("Farm A", "NGN"));

        var inactive = async () => await service.UpsertCatalogItemAsync(
            new UpsertSupplierIngredientCommand(supplier.Id, stale, 10m, 5_000m));
        await inactive.Should().ThrowAsync<InvalidOperationException>().WithMessage("*inactive*");

        var missing = async () => await service.UpsertCatalogItemAsync(
            new UpsertSupplierIngredientCommand(supplier.Id, Guid.NewGuid(), 10m, 5_000m));
        await missing.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task UpsertCatalogItem_Should_Reject_WhenSupplierInactive()
    {
        var (service, ctx, tenantId) = Build();
        await using var _ = ctx;
        var rice = await SeedIngredientAsync(ctx, tenantId);
        var supplier = await service.CreateAsync(new CreateSupplierCommand("Farm A", "NGN"));
        await service.UpdateAsync(new UpdateSupplierCommand(supplier.Id, "Farm A", "NGN", IsActive: false));

        var act = async () => await service.UpsertCatalogItemAsync(
            new UpsertSupplierIngredientCommand(supplier.Id, rice, 25m, 25_000m));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*inactive*");
    }

    [Fact]
    public async Task ListSuppliersForIngredient_Should_ReturnCheapestPerBaseUnitFirst()
    {
        var (service, ctx, tenantId) = Build();
        await using var _ = ctx;
        var rice = await SeedIngredientAsync(ctx, tenantId);
        var farmA = await service.CreateAsync(new CreateSupplierCommand("Farm A", "NGN"));
        var farmB = await service.CreateAsync(new CreateSupplierCommand("Farm B", "NGN"));

        await service.UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(farmA.Id, rice, 25m, 30_000m)); // 1,200/kg
        await service.UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(farmB.Id, rice, 50m, 45_000m)); // 900/kg

        var rows = await service.ListSuppliersForIngredientAsync(rice);

        rows.Should().HaveCount(2);
        rows[0].SupplierId.Should().Be(farmB.Id);
        rows[0].SupplierName.Should().Be("Farm B");
        rows[0].UnitPrice.Should().Be(900m);
        rows[1].SupplierId.Should().Be(farmA.Id);
        rows[1].UnitPrice.Should().Be(1_200m);
    }

    [Fact]
    public async Task ListCatalog_Should_ReturnOnlyTheSuppliersRows()
    {
        var (service, ctx, tenantId) = Build();
        await using var _ = ctx;
        var rice = await SeedIngredientAsync(ctx, tenantId, "Rice");
        var beans = await SeedIngredientAsync(ctx, tenantId, "Beans");
        var farmA = await service.CreateAsync(new CreateSupplierCommand("Farm A", "NGN"));
        var farmB = await service.CreateAsync(new CreateSupplierCommand("Farm B", "NGN"));

        await service.UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(farmA.Id, rice, 25m, 25_000m));
        await service.UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(farmA.Id, beans, 10m, 12_000m));
        await service.UpsertCatalogItemAsync(new UpsertSupplierIngredientCommand(farmB.Id, rice, 50m, 45_000m));

        var catalog = await service.ListCatalogAsync(farmA.Id);

        catalog.Should().HaveCount(2);
        catalog.Select(c => c.IngredientName).Should().ContainInOrder("Beans", "Rice"); // ordered by ingredient name
        catalog.Should().OnlyContain(c => c.SupplierId == farmA.Id);
    }
}
