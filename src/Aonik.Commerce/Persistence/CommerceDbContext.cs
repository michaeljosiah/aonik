using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Entities.Production;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Persistence;

/// <summary>
/// Module-scoped DbContext for the Commerce domain (Spec 042 §15). Shares the same physical SQL
/// Server database as <c>AonikDbContext</c> (the canonical migration stream) — module DbContexts
/// are runtime-only DI scoping and declare <strong>no</strong> migrations, per ADR-005. Tables use
/// the <c>Ank</c> prefix in <c>dbo</c>.
/// </summary>
internal sealed class CommerceDbContext : AonikDbContextBase
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductMedia> ProductMedia => Set<ProductMedia>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<BundleSlot> BundleSlots => Set<BundleSlot>();
    public DbSet<BundleSlotOption> BundleSlotOptions => Set<BundleSlotOption>();
    public DbSet<InventoryLevel> InventoryLevels => Set<InventoryLevel>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<Entities.Cart.Cart> Carts => Set<Entities.Cart.Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<CartItemSelection> CartItemSelections => Set<CartItemSelection>();
    public DbSet<OrderBundleSelection> OrderBundleSelections => Set<OrderBundleSelection>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<OrderChargeSummary> OrderChargeSummaries => Set<OrderChargeSummary>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientCost> IngredientCosts => Set<IngredientCost>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeComponent> RecipeComponents => Set<RecipeComponent>();

    public CommerceDbContext(
        DbContextOptions<CommerceDbContext> options,
        ITenantProvider? tenantProvider = null,
        ICurrentUserProvider? currentUserProvider = null,
        IClock? clock = null)
        : base(options, tenantProvider, currentUserProvider, clock)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaNames.Default);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceDbContext).Assembly);

        ApplyDboPrefixedTableNames(modelBuilder);
        ConfigureRowVersions(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        MapTable<Product>(modelBuilder, "Products");
        MapTable<ProductVariant>(modelBuilder, "ProductVariants");
        MapTable<ProductCategory>(modelBuilder, "ProductCategories");
        MapTable<ProductMedia>(modelBuilder, "ProductMedia");
        MapTable<ProductPrice>(modelBuilder, "ProductPrices");
        MapTable<BundleSlot>(modelBuilder, "BundleSlots");
        MapTable<BundleSlotOption>(modelBuilder, "BundleSlotOptions");
        MapTable<InventoryLevel>(modelBuilder, "InventoryLevels");
        MapTable<InventoryReservation>(modelBuilder, "InventoryReservations");
        MapTable<Entities.Cart.Cart>(modelBuilder, "Carts");
        MapTable<CartItem>(modelBuilder, "CartItems");
        MapTable<CartItemSelection>(modelBuilder, "CartItemSelections");
        MapTable<OrderBundleSelection>(modelBuilder, "OrderBundleSelections");
        MapTable<Discount>(modelBuilder, "Discounts");
        MapTable<OrderChargeSummary>(modelBuilder, "OrderChargeSummaries");
        MapTable<Ingredient>(modelBuilder, "Ingredients");
        MapTable<IngredientCost>(modelBuilder, "IngredientCosts");
        MapTable<Recipe>(modelBuilder, "Recipes");
        MapTable<RecipeComponent>(modelBuilder, "RecipeComponents");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
        => modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Default}{tableName}", SchemaNames.Default);
}
