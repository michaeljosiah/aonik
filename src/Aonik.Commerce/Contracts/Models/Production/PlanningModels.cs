namespace Aonik.Commerce.Contracts.Models.Production;

// Spec 055 — production planning read models. There is NO persisted entity behind these: the
// production sheet and the prep list are projections computed on read from ProductPurchase orders
// (via the SharedKernel Ordering contract), Commerce bundle selections, Spec 050 recipes, and
// Spec 052 stock levels. No DbSet, no migration.

/// <summary>The planning window (Spec 055 §12): two UTC instants, half-open — an order counts when
/// <c>FromUtc &lt;= Order.CreatedAt &lt; ToUtc</c> — so adjacent windows never double-count a
/// boundary order. v1 is an ad-hoc range; a persisted/named window is a Spec 055 Open follow-up.</summary>
public record ProductionWindow(DateTime FromUtc, DateTime ToUtc);

/// <summary>One production-sheet line: portion demand for one product variant (Spec 055 §9).
/// Names are resolved from the live catalog with LEFT-join semantics — a variant that no longer
/// resolves (deleted, foreign id) still shows, with diagnostic placeholder names, never dropped.</summary>
public record ProductionSheetLineDto(
    Guid ProductVariantId,
    string ProductName,
    string VariantName,
    decimal PortionsDemanded,
    int OrderCount,
    /// Spec 068 §9 — demand groups by (variant, canonical personalisation): two differently-
    /// personalised preparations of one variant are two lines. Null = unpersonalised demand.
    string? PersonalisationJson = null,
    string? PersonalisationSummary = null,
    /// Label-snapshotted Spec 066 §12 display entries, frozen at checkout (raw JSON).
    string? PersonalisationDisplayJson = null);

/// <summary>The aggregated production sheet for a window (Spec 055 §9): "what must the kitchen
/// make, and how many portions of each". <see cref="TotalOrders"/> counts the orders admitted by
/// the §9 inclusion filter; <see cref="BundleLinesExpanded"/> counts the build-your-own-box order
/// lines that were expanded into their chosen component variants (Spec 042 §12 Option A).</summary>
public record ProductionSheetDto(
    ProductionWindow Window,
    IReadOnlyList<ProductionSheetLineDto> Lines,
    int TotalOrders,
    int BundleLinesExpanded);

/// <summary>One prep-list line: the required quantity of an ingredient, in its base unit
/// (Spec 055 §10/§11). The netting fields are null when the caller asked for raw requirements
/// (<c>netAgainstStock = false</c>); when netting, <see cref="Available"/> is Spec 052's
/// OnHand − Reserved (never raw on-hand), <see cref="Shortfall"/> is
/// max(Required − Available, 0), and <see cref="SuggestedOrderQuantity"/> covers the shortfall
/// using the Spec 053 seed precedence (ReorderQuantity as-is; else cheapest-catalog whole packs;
/// else the shortfall itself) — null when there is no shortfall.</summary>
public record PrepListLineDto(
    Guid IngredientId,
    string IngredientName,
    string BaseUnit,
    decimal RequiredQuantity,
    decimal? Available,
    decimal? Shortfall,
    decimal? SuggestedOrderQuantity);

/// <summary>The ingredient prep list for a window (Spec 055 §10): the production sheet exploded
/// through active recipes via Spec 050's <c>ExplodeManyAsync</c>, merged per ingredient.
/// <see cref="VariantsWithoutRecipe"/> surfaces the Spec 050 no-recipe diagnostic — a demanded
/// variant with no active recipe is reported, never silently under-counted.</summary>
public record PrepListDto(
    ProductionWindow Window,
    IReadOnlyList<PrepListLineDto> Lines,
    IReadOnlyList<Guid> VariantsWithoutRecipe,
    bool NettedAgainstStock);
