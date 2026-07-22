using System.Text.Json;

namespace Aonik.Commerce.Contracts.Models.Catalog;

/// <summary>
/// The single document of storefront tunables the frontend must never hard-code (Spec 070 §9).
/// Served anonymously, cacheable, tenant-partitioned; an unconfigured storefront gets a valid
/// minimal document — the endpoint never 404s.
/// </summary>
public record StorefrontConfigDto(
    /// The canonical tenant currency (Tenant.DefaultCurrency) labelling the settings-derived
    /// amounts. "GBP" only as the last-resort fallback when the tenant record carries none.
    string Currency,
    string RecommendedChoiceLabel,
    int ResultsPageSize,
    /// Storefront-defined JSON object served verbatim, e.g. {"type":"cardIndex","value":10}.
    JsonElement BackToTopTrigger,
    StorefrontDeliveryDto Delivery,
    string? DefaultBoxSlug,
    /// The default box bundle's embedded Spec 068 size plan. Null when unset — or until 068 is
    /// live; the two states are indistinguishable by design, and the frontend treats both as
    /// "no box plan to render".
    StorefrontBoxPlanDto? Box);

/// <summary>Delivery DISPLAY amounts. ListAmount is what the storefront shows (e.g. struck
/// through); ChargedAmount is what checkout actually charges (0 renders as free delivery).</summary>
public record StorefrontDeliveryDto(decimal ListAmount, decimal ChargedAmount);

/// <summary>The Spec 068 size plan of the default box bundle. Carries the plan's OWN currency —
/// when it differs from the document's top-level currency, both serve verbatim so the operator
/// sees the misconfiguration rather than either side being silently re-labelled (§9).</summary>
public record StorefrontBoxPlanDto(
    int MinSize,
    int MaxSize,
    string Currency,
    decimal? PerSpacePrice,
    IReadOnlyList<StorefrontBoxPresetDto> Presets);

public record StorefrontBoxPresetDto(int Size, decimal Price, string? Badge, string? Blurb, decimal? Saving);

/// <summary>Typed write for the Commerce.Storefront.* settings. Null members leave the stored
/// setting unchanged; an explicit empty string clears a tenant override back to the default.</summary>
public record UpdateStorefrontConfigCommand(
    string? RecommendedChoiceLabel = null,
    int? ResultsPageSize = null,
    string? BackToTopTriggerJson = null,
    decimal? DeliveryListAmount = null,
    decimal? DeliveryChargedAmount = null,
    string? DefaultBoxSlug = null);
