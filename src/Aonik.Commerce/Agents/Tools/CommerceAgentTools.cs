using System.ComponentModel;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Inventory;
using Aonik.Commerce.Services.Production;
using Aonik.Commerce.Services.Sourcing;

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

    private CommerceAgentTools(
        IProductService products,
        IProductPricingService pricing,
        IInventoryService inventory,
        ICartService carts,
        ICheckoutService checkout,
        IIngredientService ingredients,
        IRecipeService recipes)
    {
        _products = products;
        _pricing = pricing;
        _inventory = inventory;
        _carts = carts;
        _checkout = checkout;
        _ingredients = ingredients;
        _recipes = recipes;
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

    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new CommerceAgentTools(
            serviceProvider.GetRequiredService<IProductService>(),
            serviceProvider.GetRequiredService<IProductPricingService>(),
            serviceProvider.GetRequiredService<IInventoryService>(),
            serviceProvider.GetRequiredService<ICartService>(),
            serviceProvider.GetRequiredService<ICheckoutService>(),
            serviceProvider.GetRequiredService<IIngredientService>(),
            serviceProvider.GetRequiredService<IRecipeService>());

        // Read — direct execution.
        yield return AIFunctionFactory.Create(tools.SearchProducts, name: "commerce_search_products");
        yield return AIFunctionFactory.Create(tools.GetProduct, name: "commerce_get_product");
        yield return AIFunctionFactory.Create(tools.ViewCart, name: "commerce_view_cart");
        yield return AIFunctionFactory.Create(tools.CheckInventory, name: "commerce_check_inventory");
        yield return AIFunctionFactory.Create(tools.ListIngredients, name: "commerce_list_ingredients");
        yield return AIFunctionFactory.Create(tools.GetRecipe, name: "commerce_get_recipe");
        yield return AIFunctionFactory.Create(tools.ExplodeRecipe, name: "commerce_explode_recipe");

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
    }
}
