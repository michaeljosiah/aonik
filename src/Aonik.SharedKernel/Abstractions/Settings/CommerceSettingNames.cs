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
}
