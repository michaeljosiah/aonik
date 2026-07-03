using System.ComponentModel;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Inventory;
using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Contracts.Models.Reporting;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Production;
using Aonik.Commerce.Services.Reporting;
using Aonik.Commerce.Services.Sourcing;
using Aonik.SharedKernel.Abstractions.Ordering;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Commerce.Agents.Tools;

/// <summary>
/// AI agent tools for the Commerce module (Spec 042 §13). Read tools execute directly; mutating
/// tools are classified by <c>CommerceToolApprovalManifest</c> and wrapped by the central
/// <c>IToolApprovalGate</c> before they reach the model — low (cart writes) run in-band, medium
/// (catalog/price/inventory/checkout) require an in-session confirmation. Money capture is never a
/// Commerce tool.
/// </summary>
internal sealed class CommerceAgentTools
{
    private readonly IProductService _products;
    private readonly IProductPricingService _pricing;
    private readonly IInventoryService _inventory;
    private readonly ICartService _carts;
    private readonly ICheckoutService _checkout;
    private readonly IIngredientService _ingredients;
    private readonly IRecipeService _recipes;
    private readonly IIngredientCostService _ingredientCosts;
    private readonly IProductCostingService _costing;
    private readonly ILowStockAlertService _lowStockAlerts;
    private readonly ISupplierService _suppliers;
    private readonly IPurchaseOrderService _purchaseOrders;
    private readonly IGoodsReceiptService _goodsReceipts;
    private readonly IProductionPlanningService _planning;
    private readonly IProductionOrderService _productionOrders;
    private readonly IMarginReportService _margins;

    private CommerceAgentTools(
        IProductService products,
        IProductPricingService pricing,
        IInventoryService inventory,
        ICartService carts,
        ICheckoutService checkout,
        IIngredientService ingredients,
        IRecipeService recipes,
        IIngredientCostService ingredientCosts,
        IProductCostingService costing,
        ILowStockAlertService lowStockAlerts,
        ISupplierService suppliers,
        IPurchaseOrderService purchaseOrders,
        IGoodsReceiptService goodsReceipts,
        IProductionPlanningService planning,
        IProductionOrderService productionOrders,
        IMarginReportService margins)
    {
        _products = products;
        _pricing = pricing;
        _inventory = inventory;
        _carts = carts;
        _checkout = checkout;
        _ingredients = ingredients;
        _recipes = recipes;
        _ingredientCosts = ingredientCosts;
        _costing = costing;
        _lowStockAlerts = lowStockAlerts;
        _suppliers = suppliers;
        _purchaseOrders = purchaseOrders;
        _goodsReceipts = goodsReceipts;
        _planning = planning;
        _productionOrders = productionOrders;
        _margins = margins;
    }

    // ── Read ──────────────────────────────────────────────────────────────────────────────────

    [Description("Searches the product catalog. Optionally filter by kind (Simple, Variant, Bundle) or a name/slug search term.")]
    public Task<PagedResult<ProductSummaryDto>> SearchProducts(
        [Description("Optional product kind: Simple, Variant, or Bundle")] string? kind = null,
        [Description("Optional name/slug search term")] string? search = null,
        [Description("Page number (1-based)")] int page = 1,
        CancellationToken cancellationToken = default)
        => _products.ListProductsAsync(new ListProductsQuery(Kind: kind, Search: search, Page: page), cancellationToken);

    [Description("Gets full product detail (variants, prices, media, and bundle slots) by product id.")]
    public Task<ProductDto?> GetProduct(
        [Description("The product id (GUID)")] Guid productId,
        CancellationToken cancellationToken = default)
        => _products.GetProductAsync(productId, cancellationToken);

    [Description("Views a cart with its lines and totals by cart id.")]
    public Task<CartDto?> ViewCart(
        [Description("The cart id (GUID)")] Guid cartId,
        CancellationToken cancellationToken = default)
        => _carts.GetCartAsync(cartId, cancellationToken);

    [Description("Checks the available stock for a product variant. Returns the number of units available.")]
    public Task<decimal> CheckInventory(
        [Description("The product variant id (GUID)")] Guid productVariantId,
        CancellationToken cancellationToken = default)
        => _inventory.GetAvailableAsync(productVariantId, cancellationToken);

    [Description("Lists the tenant's ingredients (raw materials) with their base units, ordered by name.")]
    public Task<IReadOnlyList<IngredientDto>> ListIngredients(
        [Description("Include deactivated ingredients (default false)")] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => _ingredients.ListAsync(includeInactive, cancellationToken);

    [Description("Gets the active recipe (bill of materials) for a product variant, including each ingredient component with its quantity and base unit. Returns null when the variant has no recipe.")]
    public Task<RecipeDto?> GetRecipe(
        [Description("The product variant id (GUID)")] Guid productVariantId,
        CancellationToken cancellationToken = default)
        => _recipes.GetRecipeAsync(productVariantId, cancellationToken);

    [Description("Explodes a product variant's recipe into the required ingredient quantities for a number of portions. When the variant has no active recipe, HasActiveRecipe is false and the lines are empty.")]
    public Task<RecipeExplosionDto> ExplodeRecipe(
        [Description("The product variant id (GUID)")] Guid productVariantId,
        [Description("The number of portions (yield-units) to produce")] decimal portions,
        CancellationToken cancellationToken = default)
        => _recipes.ExplodeAsync(productVariantId, portions, cancellationToken);

    [Description("Rolls up a product variant's standard cost (food cost per portion) in a currency: the active recipe valued at each ingredient's current cost. When the variant has no recipe, or any component lacks a cost in that currency, the result is flagged and UnitCost is null — never a silent zero.")]
    public Task<StandardCostDto> GetProductCost(
        [Description("The product variant id (GUID)")] Guid productVariantId,
        [Description("ISO 4217 currency code (e.g. NGN, GBP)")] string currency,
        CancellationToken cancellationToken = default)
        => _costing.RollupStandardCostAsync(productVariantId, currency, null, cancellationToken);

    [Description("Checks an ingredient's (raw material's) stock: on-hand, reserved, available, and the reorder point/quantity if set. Quantities are in the ingredient's base unit.")]
    public Task<StockLevelDto> CheckIngredientStock(
        [Description("The ingredient id (GUID)")] Guid ingredientId,
        CancellationToken cancellationToken = default)
        => _inventory.GetStockLevelAsync(StockItemRef.Ingredient(ingredientId), cancellationToken);

    [Description("Lists the tenant's active (Open or Acknowledged) low-stock alerts — ingredients whose available stock is at or below their reorder point.")]
    public async Task<IReadOnlyList<LowStockAlertDto>> ListLowStock(
        CancellationToken cancellationToken = default)
    {
        var alerts = await _lowStockAlerts.ListAsync(status: null, cancellationToken);
        return alerts
            .Where(a => a.Status is LowStockAlertStatuses.Open or LowStockAlertStatuses.Acknowledged)
            .ToList();
    }

    [Description("Lists the tenant's suppliers (counterparties we buy raw materials from), with currency, lead time, and payment terms.")]
    public Task<IReadOnlyList<SupplierDto>> ListSuppliers(
        [Description("Include deactivated suppliers (default false)")] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => _suppliers.ListAsync(includeInactive, cancellationToken);

    [Description("Builds the production sheet for a UTC window: per-product-variant portion demand aggregated from committed (paid or in-fulfilment, never draft or cancelled) product-purchase orders created in [fromUtc, toUtc). Build-your-own-box lines are expanded into their chosen component variants.")]
    public Task<ProductionSheetDto> GetProductionSheet(
        [Description("Window start (UTC, inclusive)")] DateTime fromUtc,
        [Description("Window end (UTC, exclusive)")] DateTime toUtc,
        CancellationToken cancellationToken = default)
        => _planning.GetProductionSheetAsync(new ProductionWindow(fromUtc, toUtc), cancellationToken);

    [Description("Builds the ingredient prep list for a UTC window: the production sheet exploded through active recipes into per-ingredient required quantities in base units. By default each line is netted against available stock (on-hand minus reserved) with a shortfall and a suggested order quantity; variants without a recipe are flagged, never silently under-counted.")]
    public Task<PrepListDto> GetPrepList(
        [Description("Window start (UTC, inclusive)")] DateTime fromUtc,
        [Description("Window end (UTC, exclusive)")] DateTime toUtc,
        [Description("Net requirements against available stock (default true); false returns raw requirements only")] bool netAgainstStock = true,
        CancellationToken cancellationToken = default)
        => _planning.GetPrepListAsync(new ProductionWindow(fromUtc, toUtc), netAgainstStock, cancellationToken);

    [Description("Gets the kitchen sheet for a production order: per-dish prep detail (each ingredient's per-portion and total quantity, from the recipe snapshot frozen when the order was created) plus a merged all-ingredients totals bill. The numbers are exactly what releasing the order will consume. Returns null when the order does not exist.")]
    public Task<KitchenSheetDto?> GetKitchenSheet(
        [Description("The production order id (GUID)")] Guid productionOrderId,
        CancellationToken cancellationToken = default)
        => _productionOrders.GetKitchenSheetAsync(productionOrderId, cancellationToken);

    [Description("Builds the margin & profit report for a UTC window in a currency: per product variant sold — quantity, discounted revenue, standard food cost (COGS), gross margin, margin %, and a below-target flag — plus the window aggregate. Only PAYMENT-COMPLETED product-purchase orders created in [fromUtc, toUtc) count as revenue (unpaid checkouts never do). Build-your-own-box lines are expanded into their chosen components. A variant with no recipe or a missing ingredient cost is surfaced as COGS-unknown and excluded from the aggregate margin — never counted as zero cost.")]
    public Task<MarginReportDto> GetMarginReport(
        [Description("Window start (UTC, inclusive)")] DateTime fromUtc,
        [Description("Window end (UTC, exclusive)")] DateTime toUtc,
        [Description("ISO 4217 report currency (e.g. NGN, GBP); orders and costs in other currencies are skipped and flagged, never converted")] string currency,
        CancellationToken cancellationToken = default)
        => _margins.GetMarginReportAsync(new ProductionWindow(fromUtc, toUtc), currency, cancellationToken);

    // ── Low — reversible cart writes ────────────────────────────────────────────────────────────

    [Description("Creates a shopping cart in the given currency. Returns the new cart.")]
    public Task<CartDto> CreateCart(
        [Description("ISO 4217 currency code (e.g. NGN, USD)")] string currency,
        [Description("Optional buyer party id (GUID); omit for a guest cart")] Guid? buyerPartyId = null,
        CancellationToken cancellationToken = default)
        => _carts.CreateCartAsync(new CreateCartCommand(currency, buyerPartyId), cancellationToken);

    [Description("Adds a simple product line to a cart.")]
    public Task<CartDto> AddToCart(
        [Description("The cart id (GUID)")] Guid cartId,
        [Description("The product variant id (GUID)")] Guid productVariantId,
        [Description("Quantity to add")] decimal quantity,
        CancellationToken cancellationToken = default)
        => _carts.AddItemAsync(new AddCartItemCommand(cartId, productVariantId, quantity), cancellationToken);

    [Description("Adds a build-your-own-box selection to a cart as a single bundle line. The selection lists the chosen component variants per slot.")]
    public Task<CartDto> AddBundleToCart(
        [Description("The cart id (GUID)")] Guid cartId,
        [Description("The bundle product id (GUID)")] Guid bundleProductId,
        [Description("The chosen components: each item is a bundle slot id, a product variant id, and a quantity")] List<BundleSelectionLine> selection,
        CancellationToken cancellationToken = default)
        => _carts.AddBundleAsync(new AddBundleToCartCommand(cartId, bundleProductId, selection), cancellationToken);

    // ── Medium — everyday domain writes + checkout ──────────────────────────────────────────────

    [Description("Creates a catalog product. Kind is Simple, Variant, or Bundle.")]
    public Task<ProductDto> CreateProduct(
        [Description("URL slug (unique per tenant)")] string slug,
        [Description("Display name")] string name,
        [Description("Product kind: Simple, Variant, or Bundle")] string kind,
        [Description("Optional description")] string? description = null,
        CancellationToken cancellationToken = default)
        => _products.CreateProductAsync(new CreateProductCommand(slug, name, kind, description ?? string.Empty), cancellationToken);

    [Description("Sets the active price for a product variant in a currency.")]
    public Task<ProductPriceDto> SetPrice(
        [Description("The product variant id (GUID)")] Guid productVariantId,
        [Description("ISO 4217 currency code")] string currency,
        [Description("Price amount")] decimal amount,
        CancellationToken cancellationToken = default)
        => _pricing.SetPriceAsync(new SetPriceCommand(productVariantId, currency, amount), cancellationToken);

    [Description("Sets the on-hand stock quantity for a product variant.")]
    public async Task<string> AdjustInventory(
        [Description("The product variant id (GUID)")] Guid productVariantId,
        [Description("New on-hand quantity")] decimal onHand,
        CancellationToken cancellationToken = default)
    {
        await _inventory.SetOnHandAsync(productVariantId, onHand, cancellationToken);
        var available = await _inventory.GetAvailableAsync(productVariantId, cancellationToken);
        return $"Variant {productVariantId} on-hand set to {onHand}; {available} now available.";
    }

    [Description("Checks out a cart: reserves stock, creates the product-purchase order, and initiates a draft guest payment. Does NOT capture money.")]
    public Task<CheckoutResult> Checkout(
        [Description("The cart id (GUID)")] Guid cartId,
        [Description("The payment provider code (e.g. Stripe, Paystack)")] string provider,
        [Description("The payment method type (e.g. Card, BankTransfer)")] string paymentMethodType,
        CancellationToken cancellationToken = default)
        => _checkout.CheckoutAsync(new CheckoutCommand(cartId, provider, paymentMethodType), cancellationToken);

    [Description("Creates an ingredient (raw material) in the tenant's master. The base unit (kg, g, L, ml, or each) is the single unit all recipe quantities for this ingredient use.")]
    public Task<IngredientDto> CreateIngredient(
        [Description("Ingredient name (unique per tenant)")] string name,
        [Description("Base unit of measure: kg, g, L, ml, or each")] string baseUnit,
        [Description("Optional SKU / internal code (unique per tenant where set)")] string? sku = null,
        [Description("Optional category (e.g. Produce, Meat, Dry goods)")] string? category = null,
        CancellationToken cancellationToken = default)
        => _ingredients.CreateAsync(new CreateIngredientCommand(name, baseUnit, sku, category), cancellationToken);

    [Description("Defines (or replaces) the recipe — the bill of materials — for a product variant. Each component is an ingredient id plus the quantity, in that ingredient's base unit, consumed per yield. Replacing overwrites the existing recipe's components.")]
    public Task<RecipeDto> SetRecipe(
        [Description("The product variant id (GUID) the recipe produces")] Guid productVariantId,
        [Description("Recipe display name")] string name,
        [Description("How many yield-units (e.g. portions) one run of the recipe produces")] decimal yieldQuantity,
        [Description("The yield unit, e.g. portion")] string yieldUnit,
        [Description("The components: each item is an ingredient id, a quantity in that ingredient's base unit, and optional notes")] List<RecipeComponentCommand> components,
        CancellationToken cancellationToken = default)
        => _recipes.SetRecipeAsync(new SetRecipeCommand(productVariantId, name, yieldQuantity, yieldUnit, components), cancellationToken);

    [Description("Sets a new unit cost for an ingredient (per its base unit) in a currency — 'a supplier repriced'. The prior cost is closed and preserved as history, never overwritten. An optional future effectiveFrom schedules the cost to take effect on that date.")]
    public Task<IngredientCostDto> UpdateIngredientCost(
        [Description("The ingredient id (GUID)")] Guid ingredientId,
        [Description("ISO 4217 currency code (e.g. NGN, GBP)")] string currency,
        [Description("The new unit cost, per the ingredient's base unit (e.g. cost per kg)")] decimal unitCost,
        [Description("Optional UTC date the cost takes effect; omit for now. A future date stores a scheduled cost that does not apply until then.")] DateTime? effectiveFrom = null,
        CancellationToken cancellationToken = default)
        => _ingredientCosts.SetCostAsync(new SetIngredientCostCommand(ingredientId, currency, unitCost, effectiveFrom), cancellationToken);

    [Description("Sets an ingredient's (raw material's) on-hand stock quantity, in its base unit (admin stock adjustment).")]
    public async Task<string> SetIngredientStock(
        [Description("The ingredient id (GUID)")] Guid ingredientId,
        [Description("New on-hand quantity, in the ingredient's base unit")] decimal onHand,
        CancellationToken cancellationToken = default)
    {
        var item = StockItemRef.Ingredient(ingredientId);
        await _inventory.SetOnHandAsync(item, onHand, cancellationToken);
        var level = await _inventory.GetStockLevelAsync(item, cancellationToken);
        return $"Ingredient {ingredientId} on-hand set to {onHand}; {level.Available} now available.";
    }

    [Description("Registers a supplier — a counterparty we buy raw materials from. Currency is the ISO 4217 code we buy in.")]
    public Task<SupplierDto> CreateSupplier(
        [Description("Supplier name (unique per tenant)")] string name,
        [Description("ISO 4217 currency code we buy from this supplier in (e.g. NGN, GBP)")] string currency,
        [Description("Optional default lead time in days")] int? leadTimeDays = null,
        [Description("Optional free-text payment terms, e.g. 'Net 30'")] string? paymentTerms = null,
        CancellationToken cancellationToken = default)
        => _suppliers.CreateAsync(new CreateSupplierCommand(name, currency, PartyId: null, leadTimeDays, paymentTerms), cancellationToken);

    [Description("Creates a Draft purchase order to a supplier for raw materials — an Order on the shared spine (we pay; the supplier is the payee). Line quantities are in each ingredient's base unit; omit a line's unit price to default from the supplier's catalog (pack price / pack size). Does NOT submit the order and never moves money.")]
    public Task<OrderDto> CreatePurchaseOrder(
        [Description("The supplier id (GUID)")] Guid supplierId,
        [Description("The lines: each item is an ingredient id, a quantity in that ingredient's base unit, and an optional explicit unit price")] List<PurchaseOrderLineCommand> lines,
        [Description("Optional ISO 4217 currency; omit for the supplier's currency")] string? currency = null,
        [Description("Optional free-text note for the order")] string? notes = null,
        CancellationToken cancellationToken = default)
        => _purchaseOrders.CreateAsync(new CreatePurchaseOrderCommand(supplierId, lines, currency, notes), cancellationToken);

    [Description("Submits a Draft purchase order to the supplier (Draft -> Pending). Records the placement only; paying the supplier is a separate, deferred action.")]
    public Task<OrderDto> SubmitPurchaseOrder(
        [Description("The purchase order id (GUID)")] Guid orderId,
        CancellationToken cancellationToken = default)
        => _purchaseOrders.SubmitAsync(orderId, cancellationToken);

    [Description("Receives goods against a submitted (Pending) purchase order, fully or partially: increments each ingredient's on-hand stock, records the actual unit cost paid when given (superseding the prior cost from the receipt date), resolves low-stock alerts that recovered above their reorder point, and completes the purchase order when every line is fully received. Never moves money. Retrying the SAME delivery must reuse the SAME idempotency key — a retried key returns the existing receipt without double-counting stock.")]
    public Task<GoodsReceiptDto> ReceiveGoods(
        [Description("The purchase order id (GUID); must be submitted (Pending)")] Guid orderId,
        [Description("A key that uniquely identifies this physical delivery (e.g. 'po-<id>-delivery-1'); reuse it when retrying the same delivery")] string idempotencyKey,
        [Description("The received lines: each item is an ingredient id, the quantity received in that ingredient's base unit, and an optional actual unit cost paid (per base unit, in the purchase order's currency)")] List<ReceiveGoodsLineCommand> lines,
        [Description("Optional UTC time the goods arrived; omit for now")] DateTime? receivedAt = null,
        CancellationToken cancellationToken = default)
        => _goodsReceipts.ReceiveAsync(new ReceiveGoodsCommand(orderId, idempotencyKey, lines, receivedAt), cancellationToken);

    [Description("Sets an ingredient's reorder point — the available quantity at or below which a low-stock alert is raised — and an optional suggested reorder quantity. Pass no reorder point to clear alerting for the ingredient.")]
    public async Task<string> SetReorderPoint(
        [Description("The ingredient id (GUID)")] Guid ingredientId,
        [Description("Alert when available stock is at or below this quantity (base unit); omit to clear alerting")] decimal? reorderPoint = null,
        [Description("Optional suggested top-up quantity for the eventual purchase order")] decimal? reorderQuantity = null,
        CancellationToken cancellationToken = default)
    {
        var level = await _inventory.SetReorderPointAsync(StockItemRef.Ingredient(ingredientId), reorderPoint, reorderQuantity, cancellationToken);
        return level.ReorderPoint is null
            ? $"Ingredient {ingredientId} reorder point cleared; no low-stock alerting."
            : $"Ingredient {ingredientId} reorder point set to {level.ReorderPoint}"
              + (level.ReorderQuantity is null ? "" : $" (suggested reorder quantity {level.ReorderQuantity})")
              + $"; {level.Available} currently available.";
    }

    [Description("Creates a Planned production run (a work order): the dishes (product variants) and portions to make on a date. Each line's recipe is exploded and frozen onto the line at creation — a variant without an active recipe rejects the create. Records intent only; no stock moves until the order is released.")]
    public Task<ProductionOrderDto> CreateProductionOrder(
        [Description("When the run is scheduled to be made (UTC)")] DateTime plannedFor,
        [Description("The dishes: each item is a product variant id and the portions to produce")] List<ProductionOrderLineCommand> lines,
        [Description("Optional free-text note for the run")] string? notes = null,
        CancellationToken cancellationToken = default)
        => _productionOrders.CreateAsync(new CreateProductionOrderCommand(plannedFor, lines, notes), cancellationToken);

    [Description("Releases a Planned production run — the kitchen starts, and ingredient stock is CONSUMED: every line's frozen recipe snapshot is merged into one bill and each ingredient's on-hand is drawn down, all-or-nothing. Fails (consuming nothing) if any ingredient's available stock is short. Re-releasing an already-released run is a no-op; stock is never double-consumed.")]
    public Task<ProductionOrderDto> ReleaseProductionOrder(
        [Description("The production order id (GUID)")] Guid productionOrderId,
        CancellationToken cancellationToken = default)
        => _productionOrders.ReleaseAsync(productionOrderId, cancellationToken);

    [Description("Sets (or clears) a product's target gross-margin percentage (0-100). The margin report flags any variant whose achieved margin falls below its product's target. Omit the percentage to clear the target so the product is never flagged.")]
    public Task<TargetMarginDto> SetTargetMargin(
        [Description("The product id (GUID)")] Guid productId,
        [Description("Target gross margin as a percentage between 0 and 100 (e.g. 70 for 70%); omit to clear")] decimal? targetMarginPct = null,
        CancellationToken cancellationToken = default)
        => _margins.SetTargetMarginAsync(productId, targetMarginPct, cancellationToken);

    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new CommerceAgentTools(
            serviceProvider.GetRequiredService<IProductService>(),
            serviceProvider.GetRequiredService<IProductPricingService>(),
            serviceProvider.GetRequiredService<IInventoryService>(),
            serviceProvider.GetRequiredService<ICartService>(),
            serviceProvider.GetRequiredService<ICheckoutService>(),
            serviceProvider.GetRequiredService<IIngredientService>(),
            serviceProvider.GetRequiredService<IRecipeService>(),
            serviceProvider.GetRequiredService<IIngredientCostService>(),
            serviceProvider.GetRequiredService<IProductCostingService>(),
            serviceProvider.GetRequiredService<ILowStockAlertService>(),
            serviceProvider.GetRequiredService<ISupplierService>(),
            serviceProvider.GetRequiredService<IPurchaseOrderService>(),
            serviceProvider.GetRequiredService<IGoodsReceiptService>(),
            serviceProvider.GetRequiredService<IProductionPlanningService>(),
            serviceProvider.GetRequiredService<IProductionOrderService>(),
            serviceProvider.GetRequiredService<IMarginReportService>());

        // Read — direct execution.
        yield return AIFunctionFactory.Create(tools.SearchProducts, name: "commerce_search_products");
        yield return AIFunctionFactory.Create(tools.GetProduct, name: "commerce_get_product");
        yield return AIFunctionFactory.Create(tools.ViewCart, name: "commerce_view_cart");
        yield return AIFunctionFactory.Create(tools.CheckInventory, name: "commerce_check_inventory");
        yield return AIFunctionFactory.Create(tools.ListIngredients, name: "commerce_list_ingredients");
        yield return AIFunctionFactory.Create(tools.GetRecipe, name: "commerce_get_recipe");
        yield return AIFunctionFactory.Create(tools.ExplodeRecipe, name: "commerce_explode_recipe");
        yield return AIFunctionFactory.Create(tools.GetProductCost, name: "commerce_get_product_cost");
        yield return AIFunctionFactory.Create(tools.CheckIngredientStock, name: "commerce_check_ingredient_stock");
        yield return AIFunctionFactory.Create(tools.ListLowStock, name: "commerce_list_low_stock");
        yield return AIFunctionFactory.Create(tools.ListSuppliers, name: "commerce_list_suppliers");
        yield return AIFunctionFactory.Create(tools.GetProductionSheet, name: "commerce_get_production_sheet");
        yield return AIFunctionFactory.Create(tools.GetPrepList, name: "commerce_get_prep_list");
        yield return AIFunctionFactory.Create(tools.GetKitchenSheet, name: "commerce_get_kitchen_sheet");
        yield return AIFunctionFactory.Create(tools.GetMarginReport, name: "commerce_get_margin_report");

        // Low — reversible cart writes.
        yield return AIFunctionFactory.Create(tools.CreateCart, name: "commerce_create_cart");
        yield return AIFunctionFactory.Create(tools.AddToCart, name: "commerce_add_to_cart");
        yield return AIFunctionFactory.Create(tools.AddBundleToCart, name: "commerce_add_bundle_to_cart");

        // Medium — everyday domain writes + checkout.
        yield return AIFunctionFactory.Create(tools.CreateProduct, name: "commerce_create_product");
        yield return AIFunctionFactory.Create(tools.SetPrice, name: "commerce_set_price");
        yield return AIFunctionFactory.Create(tools.AdjustInventory, name: "commerce_adjust_inventory");
        yield return AIFunctionFactory.Create(tools.Checkout, name: "commerce_checkout");
        yield return AIFunctionFactory.Create(tools.CreateIngredient, name: "commerce_create_ingredient");
        yield return AIFunctionFactory.Create(tools.SetRecipe, name: "commerce_set_recipe");
        yield return AIFunctionFactory.Create(tools.UpdateIngredientCost, name: "commerce_update_ingredient_cost");
        yield return AIFunctionFactory.Create(tools.SetIngredientStock, name: "commerce_set_ingredient_stock");
        yield return AIFunctionFactory.Create(tools.SetReorderPoint, name: "commerce_set_reorder_point");
        yield return AIFunctionFactory.Create(tools.CreateSupplier, name: "commerce_create_supplier");
        yield return AIFunctionFactory.Create(tools.CreatePurchaseOrder, name: "commerce_create_purchase_order");
        yield return AIFunctionFactory.Create(tools.SubmitPurchaseOrder, name: "commerce_submit_purchase_order");
        yield return AIFunctionFactory.Create(tools.ReceiveGoods, name: "commerce_receive_goods");
        yield return AIFunctionFactory.Create(tools.CreateProductionOrder, name: "commerce_create_production_order");
        yield return AIFunctionFactory.Create(tools.ReleaseProductionOrder, name: "commerce_release_production_order");
        yield return AIFunctionFactory.Create(tools.SetTargetMargin, name: "commerce_set_target_margin");
    }
}
