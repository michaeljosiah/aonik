namespace Aonik.Commerce.Contracts.Models.Catalog;

// ─── Resolution (Spec 067 §5/§7) ────────────────────────────────────────────

/// <summary>The §5 resolution result. Facts are authored or absent — never derived, never
/// substituted: figures may fall back to the default block (captioned via
/// <see cref="IsStandardPreparation"/>), but declarations and heating are exact-authored or
/// WITHHELD.</summary>
public record ResolvedContentDto(
    string ServingLabel,
    NutritionDto Nutrition,
    string? Ingredients,
    string? Allergens,
    /// True = no exact-authored declaration exists for this combination; the storefront shows
    /// its "not yet published for this combination" state rather than a substituted declaration.
    bool DeclarationsWithheld,
    IReadOnlyList<HeatingStepDto> Heating,
    bool HeatingWithheld,
    /// True = the DEFAULT block's figures are being shown for a DIFFERENT combination
    /// ("figures are for the standard preparation").
    bool IsStandardPreparation,
    /// True = the default block awaits review after a default-combination change (§6).
    bool IsStale,
    string CanonicalSelectionJson,
    string? MatchedVariantSelectionJson,
    int ContentVersion);

/// <summary>Published figures with nulls preserved — a tenant that publishes no sugars figure
/// serves null, never zero.</summary>
public record NutritionDto(
    decimal? Kcal,
    decimal? ProteinGrams,
    decimal? CarbsGrams,
    decimal? FatGrams,
    decimal? FibreGrams,
    decimal? SugarsGrams,
    decimal? SaltGrams);

public record HeatingStepDto(string Method, string Body);

// ─── Authoring (Spec 067 §7/§9) ─────────────────────────────────────────────

public record ProductContentDto(
    Guid ProductId,
    string ServingLabel,
    NutritionDto Nutrition,
    string? Ingredients,
    string? Allergens,
    /// <summary>Null when the stored JSON cannot be parsed — legacy damage, which the resolver
    /// WITHHOLDS rather than presenting as an authored "no heating required". The admin read
    /// reported it as an empty panel, so the two surfaces described the same row differently
    /// and an operator had no way to see that customers were being shown nothing.</summary>
    IReadOnlyList<HeatingStepDto>? Heating,
    string DescribesSelectionJson,
    bool RequiresReview,
    int ContentVersion,
    /// <summary>A token over the AUTHORED fields, for optimistic concurrency on the block.
    ///
    /// <see cref="ContentVersion"/> cannot serve: the content write pipeline is shared, so any
    /// variant create/edit/retire bumps it while the block's own text is untouched — versioning
    /// the row fabricates conflicts for unrelated writes. This changes when, and only when,
    /// something a person authored on the block changes.</summary>
    string BlockSignature);

public record ProductContentVariantDto(
    Guid Id,
    Guid ProductId,
    string SelectionJson,
    string ServingLabel,
    NutritionDto Nutrition,
    string? Ingredients,
    string? Allergens,
    IReadOnlyList<HeatingStepDto>? Heating,
    bool IsActive);

public record UpsertProductContentCommand(
    string ServingLabel,
    decimal? Kcal = null,
    decimal? ProteinGrams = null,
    decimal? CarbsGrams = null,
    decimal? FatGrams = null,
    decimal? FibreGrams = null,
    decimal? SugarsGrams = null,
    decimal? SaltGrams = null,
    string? Ingredients = null,
    string? Allergens = null,
    string? HeatingJson = null);

/// <summary>Variant authoring. <see cref="SelectionJson"/> may be partial — it is normalised
/// through Spec 066 (omitted groups filled with the then-current defaults) and stored complete.
/// Null declarations/heating mean WITHHELD for this combination, never inherited (§4).</summary>
public record UpsertContentVariantCommand(
    string SelectionJson,
    string ServingLabel,
    decimal? Kcal = null,
    decimal? ProteinGrams = null,
    decimal? CarbsGrams = null,
    decimal? FatGrams = null,
    decimal? FibreGrams = null,
    decimal? SugarsGrams = null,
    decimal? SaltGrams = null,
    string? Ingredients = null,
    string? Allergens = null,
    string? HeatingJson = null);

// ─── Coverage (Spec 067 §8) ─────────────────────────────────────────────────

/// <summary>Every authored combination plus the single-choice-deviation gaps — bounded by
/// Σ|offered choices| per product, never combinatorial. Multi-choice combinations are authored
/// on demand and appear in <see cref="Authored"/>.</summary>
/// <summary>Spec 075 dependency — the RAW authoring read: the block as stored,
/// its variants, and server-computed staleness via the resolver's own predicate
/// (<c>RequiresReview</c> OR the stored all-defaults binding no longer matching
/// the product's current default selection). Editors load THIS, never the
/// public resolution, which withholds and substitutes by design.</summary>
public record AdminProductContentDto(
    ProductContentDto? Block,
    bool IsStale,
    IReadOnlyList<ProductContentVariantDto> Variants,
    /// <summary>The all-defaults binding as of this read — the standard preparation the block
    /// WOULD be bound to if the review were confirmed now. Echoed back on confirm so the server
    /// can refuse a confirmation of a preparation the operator never saw.</summary>
    string CurrentDefaultsSelectionJson);

/// <summary>One row of the tenant content-status list (Spec 075 rail/KPIs/queue).
/// Block EXISTENCE is not publication: a block with every figure null serves no
/// nutrition, and one with neither ingredients nor allergens withholds its
/// declarations — the rail's Authored/Withheld states and the figure-serving KPI
/// need both facts, so they are projected here rather than costing a raw-content
/// request per product.</summary>
public record ContentStatusRowDto(
    Guid ProductId,
    string Slug,
    string Name,
    string ProductStatus,
    bool HasBlock,
    bool RequiresReview,
    bool IsStale,
    int VariantCount,
    /// At least one nutrition figure is published on the default block.
    bool HasFigures = false,
    /// Ingredients and/or allergens are authored on the default block.
    bool HasDeclarations = false);

public record ContentCoverageDto(
    Guid ProductId,
    IReadOnlyList<ContentCoverageEntryDto> Authored,
    IReadOnlyList<ContentCoverageGapDto> SingleChoiceGaps);

public record ContentCoverageEntryDto(Guid VariantId, string SelectionJson, bool IsActive);

/// <summary>One offered non-default choice substituted alone into the standard selection, with
/// no variant authored for the resulting combination.</summary>
public record ContentCoverageGapDto(string GroupKey, string ChoiceKey, string SelectionJson);

/// <summary>What a BLOCK write asserts about the world it was authored against (Spec 075).
///
/// Both are enforced inside the serialized write, because both describe a read-to-write window
/// no client-side check can close: whatever the editor verified before sending, the losing side
/// of a race still arrives at a service that would happily apply it.</summary>
/// <param name="ExpectedDefaultsSelectionJson">The standard preparation the operator authored
/// against, from <c>GetAdminAsync</c>. Content is bound to a preparation, so a default that
/// moved in between means these figures and declarations would publish against one nobody
/// wrote them for (V-C9).</param>
/// <param name="ExpectedBlockSignature">The block's authored-field token, or NULL asserting
/// that no block existed. The upsert is a FULL REPLACE, so a concurrent edit that this write
/// then overwrites silently erases the other operator's text — a corrected allergen declaration
/// among it (V-C10).</param>
public record BlockWritePrecondition(
    string ExpectedDefaultsSelectionJson,
    string? ExpectedBlockSignature);
