using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Commerce.Agents;

/// <summary>
/// Commerce module's tool-approval classification (Spec 042 §13 / Spec 032). Declares which commerce
/// agent tools are mutating and at what tier, so the central <see cref="IToolApprovalGate"/> wraps
/// them before they reach the model. Read tools (<c>commerce_search_products</c>, <c>_get_product</c>,
/// <c>_view_cart</c>, <c>_check_inventory</c>, <c>_list_ingredients</c>, <c>_get_recipe</c>,
/// <c>_explode_recipe</c>, <c>_get_product_cost</c>) are omitted — the gate passes unclassified, read-looking
/// tools through. Commerce never captures money, so no tool is High here (capture stays a Finance
/// high-tier action; a future <c>commerce_refund</c> would be High via a Finance proposal).
/// </summary>
internal sealed class CommerceToolApprovalManifest : IToolApprovalManifest
{
    public string Module => "Commerce";

    private static readonly IReadOnlyDictionary<string, ToolClassification> Classifications =
        new Dictionary<string, ToolClassification>(StringComparer.Ordinal)
        {
            // ── Low — reversible cart-state writes (in-band with audit) ──
            ["commerce_create_cart"] = Low("Create a shopping cart"),
            ["commerce_add_to_cart"] = Low("Add a product to a cart"),
            ["commerce_add_bundle_to_cart"] = Low("Add a build-your-own-box to a cart"),

            // ── Medium — everyday domain writes + checkout (in-session confirmation) ──
            ["commerce_create_product"] = Medium("Create a catalog product"),
            ["commerce_set_price"] = Medium("Set a product price"),
            ["commerce_adjust_inventory"] = Medium("Adjust product stock"),
            ["commerce_checkout"] = Medium("Check out a cart (creates an order + draft payment; no capture)"),

            // ── Medium — maker-ops master-data writes (Spec 050 §12) ──
            ["commerce_create_ingredient"] = Medium("Create an ingredient"),
            ["commerce_set_recipe"] = Medium("Define a product recipe"),

            // ── Medium — maker-ops costing writes (Spec 051 §11) ──
            ["commerce_update_ingredient_cost"] = Medium("Update an ingredient's unit cost"),
        };

    public ToolClassification? Classify(string toolName) =>
        Classifications.TryGetValue(toolName, out var classification) ? classification : null;

    private static ToolClassification Low(string actionKind) =>
        ToolClassification.Mutating(new ToolApprovalOptions(ToolApprovalTier.Low, actionKind));

    private static ToolClassification Medium(string actionKind) =>
        ToolClassification.Mutating(new ToolApprovalOptions(ToolApprovalTier.Medium, actionKind));
}
