namespace Aonik.SharedKernel.Abstractions.Settings;

/// <summary>
/// Well-known setting keys for the Commerce storefront. Platform registers their schemas in
/// <c>SettingDefinitions</c>; Spec 070's storefront-config document serves them to the frontend.
/// </summary>
public static class CommerceSettingNames
{
    /// <summary>
    /// Spec 066 §15 — the label a storefront renders beside a group's recommended default.
    /// Product identity is configuration, not platform code (ADR-013): the platform models a
    /// <em>recommended default</em>, and a tenant decides whether that reads "Recommended",
    /// "Abby's choice", or anything else. Visible to clients; default "Recommended".
    /// </summary>
    public const string StorefrontRecommendedChoiceLabel = "Commerce.Storefront.RecommendedChoiceLabel";

    /// <summary>Spec 070 §9 — menu page size. Visible to clients; default "8".</summary>
    public const string StorefrontResultsPageSize = "Commerce.Storefront.ResultsPageSize";

    /// <summary>Spec 070 §9 — when the storefront shows its back-to-top control, as a JSON object
    /// (e.g. <c>{"type":"cardIndex","value":10}</c>). Shape is storefront-defined; the platform
    /// stores and serves it verbatim. Visible to clients.</summary>
    public const string StorefrontBackToTopTriggerJson = "Commerce.Storefront.BackToTopTriggerJson";

    /// <summary>Spec 070 §9 — the delivery amount the storefront DISPLAYS (e.g. a struck-through
    /// "£10"). Display data, not a charge; Spec 068 quotes consume the charged amount.</summary>
    public const string StorefrontDeliveryListAmount = "Commerce.Storefront.DeliveryListAmount";

    /// <summary>Spec 070 §9 — the delivery amount actually charged. "0" renders as free delivery.</summary>
    public const string StorefrontDeliveryChargedAmount = "Commerce.Storefront.DeliveryChargedAmount";

    /// <summary>Spec 070 §9 — slug of the bundle product the storefront treats as "the box". The
    /// config document embeds that bundle's Spec 068 size plan when one exists.</summary>
    public const string StorefrontDefaultBoxProductSlug = "Commerce.Storefront.DefaultBoxProductSlug";
}
