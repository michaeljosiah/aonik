namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Known business-type values (Spec 065). An <strong>open string</strong> set — a new product is a
/// new value (typically a new pack manifest), not an edit here. Only the generic <see cref="Base"/>
/// value lives in platform code; concrete product types (e.g. <c>simi</c>, <c>food-commerce</c>) are
/// discovered from the installed pack manifests, never hard-coded as symbols here
/// (ADR-013 / Spec 064: no product noun in a platform symbol).
/// </summary>
public static class BusinessTypes
{
    /// <summary>The generic tenant with no product specialisation. The default for any tenant.</summary>
    public const string Base = "base";

    /// <summary>
    /// Normalises a caller-supplied business type: null/blank becomes <see cref="Base"/>; otherwise the
    /// value is trimmed and lower-cased so it matches a pack-manifest key deterministically.
    /// </summary>
    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Base : value.Trim().ToLowerInvariant();
}
