namespace Aonik.PersonalFinance.Services;

/// <summary>Lifecycle status values for a <c>CompassPlan</c> (Spec 021 §2).</summary>
internal static class CompassPlanStatus
{
    public const string Active = "Active";
    public const string Superseded = "Superseded";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

/// <summary>
/// Centralised normalisation/validation for Compass risk-tier strings
/// (Spec 021 §4, DEC6). V1 keeps risk tier as a validated string — <c>low</c> /
/// <c>medium</c> / <c>high</c> — rather than migrating <c>Proposal.RiskTier</c>
/// to an enum (deferred to a platform-wide spec). This is the one place Compass
/// maps/validates the value.
/// </summary>
internal static class CompassRiskTier
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";

    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        Low, Medium, High,
    };

    /// <summary>
    /// Returns the canonical lowercase tier for a (case-insensitive) input,
    /// defaulting to <see cref="Low"/> for null/blank/unknown values so a
    /// Compass recommendation never persists an out-of-vocabulary risk tier.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Low;
        }

        var trimmed = value.Trim().ToLowerInvariant();
        return Known.Contains(trimmed) ? trimmed : Low;
    }
}
