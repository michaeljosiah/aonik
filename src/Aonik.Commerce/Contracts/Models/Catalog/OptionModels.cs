using Aonik.Commerce.Entities.Catalog;

namespace Aonik.Commerce.Contracts.Models.Catalog;

// ─── Catalogue reads (admin + public option catalogue) ────────────────────────

public record OptionChoiceDto(
    Guid Id,
    string Key,
    string Label,
    string? Note,
    decimal Price,
    bool IsRecommendedDefault,
    int SortOrder,
    bool IsActive);

public record OptionGroupDto(
    Guid Id,
    string Key,
    string Label,
    string? HelpText,
    string SelectionMode,
    string Currency,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<OptionChoiceDto> Choices);

// ─── Effective options (catalogue ∩ product narrowing) ────────────────────────

/// <summary>One choice a specific product offers. Carries the absolute price; storefronts show the
/// delta against <see cref="EffectiveOptionGroupDto.DefaultChoiceKey"/>'s price.</summary>
public record EffectiveOptionChoiceDto(
    string Key,
    string Label,
    string? Note,
    decimal Price,
    int SortOrder);

/// <summary>One option group as a specific product offers it (Spec 066 §6). An empty list of these
/// on a product means it is not personalisable at all — storefronts hide the panel entirely rather
/// than rendering an empty one.</summary>
public record EffectiveOptionGroupDto(
    string Key,
    string Label,
    string? HelpText,
    string SelectionMode,
    string Currency,
    int SortOrder,
    string DefaultChoiceKey,
    IReadOnlyList<EffectiveOptionChoiceDto> Choices);

/// <summary>
/// The outcome of moving a group's recommended default. <see cref="AffectedProductSlugs"/> is the
/// point of the type: which combination counts as "the standard preparation" just changed for every
/// one of these products, so dependent capabilities (Spec 067 content review) must re-check them.
/// </summary>
public record RecommendedDefaultChangeResult(
    OptionGroupDto Group,
    IReadOnlyList<string> AffectedProductSlugs);

// ─── Selection normalisation + pricing ────────────────────────────────────────

/// <summary>Per-group contribution to the adjustment, so storefronts can show
/// "+£10 Full table · −£2 No side" without re-deriving the maths client-side.</summary>
public record OptionGroupAdjustment(string GroupKey, IReadOnlyList<string> ChosenKeys, decimal Amount);

/// <summary>A label snapshot for one group, defaults included. Persisted with orders so kitchen
/// sheets can render the human-readable preparation without the live catalogue — labels are
/// deliberately mutable, and an all-defaults order has an empty summary.</summary>
public record OptionDisplayEntry(string Group, string Choice);

/// <summary>The result of validating, canonicalising and pricing a selection (Spec 066 §10).</summary>
public record OptionSelectionResult(
    /// Complete, byte-stable canonical form (Spec 066 §7). Doubles as the cart line-merge key.
    string CanonicalSelectionJson,
    bool IsDefault,
    /// Signed, per unit. Negative when the chosen options cost less than the defaults.
    decimal Adjustment,
    string Currency,
    decimal? UnitSurcharge,
    string? UnitSurchargeCurrency,
    /// Differs-from-default text, e.g. "Full table · Salmon". Empty when <c>IsDefault</c>.
    string Summary,
    IReadOnlyList<OptionDisplayEntry> Display,
    IReadOnlyList<OptionGroupAdjustment> Breakdown);

/// <summary>One customer-visible change applied while re-normalising a stored selection.</summary>
public record SelectionDrift(string GroupKey, string? FromChoiceKey, string? ToChoiceKey, string Reason);

/// <summary>A stored selection brought back in line with the current catalogue, plus what changed.
/// Drift is reported, never thrown — otherwise retiring an option would turn every cart holding it
/// into a hard error.</summary>
public record StoredSelectionResult(OptionSelectionResult Result, IReadOnlyList<SelectionDrift> Drift);

/// <summary>Known <see cref="SelectionDrift.Reason"/> values.</summary>
public static class SelectionDriftReasons
{
    /// <summary>A chosen choice is no longer offered; remapped to the group's effective default.</summary>
    public const string OptionRetired = "option-retired";

    /// <summary>The group is no longer offered/servable; dropped from the selection. There is no
    /// effective default to remap to, so remapping would invent one.</summary>
    public const string GroupRemoved = "group-removed";

    /// <summary>The product gained a group after the selection was stored, so its default was
    /// applied. The customer never chose it, so this is reported rather than assumed silently.</summary>
    public const string GroupAdded = "group-added";

    /// <summary>The group's selection mode changed and the stored value had to be re-shaped.</summary>
    public const string SelectionModeChanged = "selection-mode-changed";
}

// ─── Authoring commands ───────────────────────────────────────────────────────

public record CreateOptionGroupCommand(
    string Key,
    string Label,
    string? HelpText = null,
    string SelectionMode = OptionSelectionModes.One,
    string Currency = "GBP",
    int SortOrder = 0);

/// <summary>Group update. <see cref="OptionGroup.Key"/> is absent by design — keys are the stable
/// contract carts and content variants match on, so they are immutable; renames are label edits.</summary>
/// <summary>Null-valued members preserve the stored value — see the request-side rationale on
/// <c>UpdateOptionGroupRequest</c>. Currency in particular denominates the group's <em>absolute</em>
/// choice prices, so defaulting it would reinterpret USD or EUR amounts as GBP without touching a
/// single number, and defaulting the mode would silently tighten a multi-select group.</summary>
public record UpdateOptionGroupCommand(
    string Label,
    string? HelpText = null,
    string? SelectionMode = null,
    string? Currency = null,
    int? SortOrder = null,
    bool? IsActive = null);

public record AddOptionChoiceCommand(
    string Key,
    string Label,
    string? Note = null,
    decimal Price = 0m,
    /// Permitted only for the 0→1 transition (a group's first default). Moving an existing default
    /// between choices must use the atomic recommended-default operation — see rule V7.
    bool IsRecommendedDefault = false,
    int SortOrder = 0,
    bool IsActive = true);

/// <summary>Choice update. Key is immutable; the default flag moves only via the atomic
/// recommended-default operation (rule V7).</summary>
/// <summary>Null-valued members preserve the stored value. The old non-null defaults were traps:
/// an update that only renamed a choice would also have repriced it to zero and — via the CLR
/// default on the request — deactivated it.</summary>
public record UpdateOptionChoiceCommand(
    string Label,
    string? Note = null,
    decimal? Price = null,
    int? SortOrder = null,
    bool? IsActive = null);

/// <summary>Spec 074 dependency — the STORED narrowing line as authored,
/// preserving the null-vs-explicit <c>AllowedChoiceKeys</c> distinction the
/// resolved effective view loses (null = inherit every active choice,
/// including future ones; a list = pinned).</summary>
public record ProductNarrowingLineDto(
    string GroupKey,
    IReadOnlyList<string>? AllowedChoiceKeys,
    string? DefaultChoiceKey,
    string? SelectionModeOverride,
    int SortOrder);

/// <summary>One group in a product's narrowing.</summary>
public record ProductOptionGroupLine(
    string GroupKey,
    /// Null = every active choice in the group.
    IReadOnlyCollection<string>? AllowedChoiceKeys = null,
    string? DefaultChoiceKey = null,
    string? SelectionModeOverride = null,
    int SortOrder = 0);

/// <summary>Full replace of a product's narrowing — idempotent. An empty list makes the product
/// not personalisable.</summary>
public record SetProductOptionGroupsCommand(IReadOnlyCollection<ProductOptionGroupLine> Groups);

/// <summary>Set or clear a product's per-unit surcharge. Currency is required when an amount is
/// given, so the stored amount can never be silently re-denominated.</summary>
public record SetUnitSurchargeCommand(decimal? Amount, string? Currency = null);
