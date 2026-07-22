using Aonik.Commerce.Agents;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Production;
using Aonik.Commerce.Services.Promotions;
using Aonik.Commerce.Services.Reporting;
using Aonik.Commerce.Services.Sourcing;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Modules;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Commerce;

/// <summary>
/// Composition-root registration for the Commerce module (Spec 042). Phase 1 wires the
/// module-scoped <see cref="CommerceDbContext"/> and the catalog + pricing services. Inventory,
/// cart/checkout, the billing/payment write contracts, and the commerce-agent land in subsequent
/// phases. The canonical migration stream stays in <c>AonikDbContext</c>.
/// </summary>
public sealed class CommerceModule : IModule
{
    public static string Name => "Commerce";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<CommerceDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"CommerceDb_{Guid.NewGuid()}";
                options.UseInMemoryDatabase(dbName);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? configuration.GetConnectionString("AonikDb")
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                options.UseSqlServer(connectionString, o => o.EnableRetryOnFailure());
            }
        });

        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductPricingService, ProductPricingService>();

        // Spec 066 — configurable product option groups: the tenant option catalogue with its
        // per-product narrowing, and the selection service that validates, canonicalises and
        // difference-prices a customer's choices (negative adjustments included).
        services.AddScoped<IProductOptionService, ProductOptionService>();
        services.AddScoped<IOptionSelectionService, OptionSelectionService>();

        // Spec 070 - storefront merchandising: curated collections, configurable filter facets,
        // and the storefront-config document (the tunables a frontend must never hard-code).
        services.AddScoped<ICollectionService, CollectionService>();
        services.AddScoped<IFacetGroupService, FacetGroupService>();
        services.AddScoped<IStorefrontConfigService, StorefrontConfigService>();

        // Spec 067 - option-dependent product content: authored default block + per-combination
        // variants, exact-selection resolution, and the default-change review reaction.
        services.AddScoped<IProductContentService, ProductContentService>();
        services.AddScoped<IProductContentReviewFlagger, ProductContentReviewFlagger>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IBundleSizePlanService, BundleSizePlanService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IDiscountService, DiscountService>();
        // Default tax seam — charges no tax. Replace with a jurisdiction-aware calculator at the
        // composition root when VAT/sales tax is required (Spec 042 §5 follow-up).
        services.AddScoped<ITaxCalculator, ZeroRateTaxCalculator>();

        // Spec 050 — maker-operations master data: the ingredient (raw-material) master and the
        // recipe / bill-of-materials with portion explosion.
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddScoped<IRecipeService, RecipeService>();

        // Spec 051 — ingredient costing: effective-dated unit costs (mirrors ProductPrice) and the
        // standard-cost rollup that values a recipe at date-aware current cost.
        services.AddScoped<IIngredientCostService, IngredientCostService>();
        services.AddScoped<IProductCostingService, ProductCostingService>();

        // Spec 052 — raw-material inventory: low-stock alerting over ingredient levels (the
        // generalized InventoryService stocks both kinds; the scan raises/refreshes one active
        // alert per ingredient and enqueues LowStockAlertRaisedEvent for the Spec 016 inbox).
        services.AddScoped<ILowStockAlertService, LowStockAlertService>();

        // Spec 053 — suppliers + supplier catalog (price list) and purchase orders. The PO is NOT
        // a Commerce entity: PurchaseOrderService composes the shared IOrderService spine
        // (OrderType "PurchaseOrder"); Commerce persists only the supplier master data.
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();

        // Spec 054 — goods receipt: the convergence write that turns a submitted PO into
        // raw-material on-hand (052 stock increment), refreshed landed cost (051), recovered-alert
        // resolution (052/054), and the PO's Complete transition on the spine (041) — idempotent
        // by a client-supplied key resolved before any mutation.
        services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();

        // Spec 055 — production planning: the production sheet (ProductPurchase demand by variant
        // over a window, read through the Ordering contract) and the prep list (that sheet exploded
        // through 050 recipes, optionally netted against 052 available stock). Pure read/aggregation
        // — no entity, no DbSet, no migration.
        services.AddScoped<IProductionPlanningService, ProductionPlanningService>();

        // Spec 056 — production / work orders + the kitchen sheet: the make-side counterpart of
        // checkout. Release consumes ingredient stock through the frozen per-line recipe snapshots
        // in one all-or-nothing commit; completion optionally yields finished-good stock; the
        // kitchen sheet is a pure read over the same snapshots.
        services.AddScoped<IProductionOrderService, ProductionOrderService>();

        // Spec 057 — the margin & profit report: revenue (payment-completed ProductPurchase
        // orders + the 042 charge summary's discounted total) vs COGS (the 051 standard-cost
        // rollup × quantity sold) vs the product's target margin. Pure read projection — the only
        // write is Product.TargetMarginPct.
        services.AddScoped<IMarginReportService, MarginReportService>();

        // Spec 042 §11 — react to PaymentCompletedEvent (commit inventory, close cart, complete
        // order). The outbox dispatcher in the Worker invokes these with the tenant restored.
        services.AddEventHandlersFromAssembly(typeof(CommerceModule).Assembly);

        // Spec 042 §13 — the commerce-agent + its tool-approval classification. The orchestrator
        // discovers IDomainAgentDescriptor via DI; the central IToolApprovalGate discovers the
        // manifest and gates every classified mutating tool (Spec 032).
        services.AddSingleton<IDomainAgentDescriptor, CommerceAgentDescriptor>();

        // Spec 065 — Commerce's first provisioning contribution: seed a starter category taxonomy
        // when a tenant's config pack enables the Commerce module (gated on the manifest, not a type).
        services.AddScoped<Aonik.SharedKernel.Abstractions.ITenantProvisioningContributor, Services.Provisioning.CommerceTenantProvisioningContributor>();
        services.AddSingleton<IToolApprovalManifest, CommerceToolApprovalManifest>();

        return services;
    }
}

public static class CommerceModuleExtensions
{
    public static IServiceCollection AddCommerceModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => CommerceModule.ConfigureServices(services, configuration);
}
