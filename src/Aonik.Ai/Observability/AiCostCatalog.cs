namespace Aonik.Ai.Observability;

/// <summary>
/// Static price catalog used by <see cref="TelemetryChatClient"/> to estimate
/// per-call USD cost. Values are intentionally simple (per 1K tokens) and
/// hard-coded — accurate enough for "did we just burn £10 of OpenAI credit?"
/// alerting without a network round-trip on every call.
///
/// Update when provider pricing changes. Unknown models return <c>0</c>.
/// </summary>
internal static class AiCostCatalog
{
    private readonly record struct Pricing(double InputPer1K, double OutputPer1K);

    // Source: https://openai.com/pricing — kept in alphabetical order.
    private static readonly Dictionary<string, Pricing> PricesPer1K = new(StringComparer.OrdinalIgnoreCase)
    {
        // OpenAI — chat completions
        ["gpt-3.5-turbo"]       = new(0.0005, 0.0015),
        ["gpt-4"]               = new(0.03,   0.06),
        ["gpt-4-turbo"]         = new(0.01,   0.03),
        ["gpt-4o"]              = new(0.0025, 0.01),
        ["gpt-4o-mini"]         = new(0.00015, 0.0006),
        ["gpt-4.1"]             = new(0.002,  0.008),
        ["gpt-4.1-mini"]        = new(0.0004, 0.0016),
        ["gpt-4.1-nano"]        = new(0.0001, 0.0004),
        ["gpt-5"]               = new(0.0125, 0.10),
        ["gpt-5-mini"]          = new(0.0025, 0.02),
        ["gpt-5-nano"]          = new(0.00005, 0.0004),
        ["o1"]                  = new(0.015,  0.06),
        ["o1-mini"]             = new(0.003,  0.012),
        ["o3-mini"]             = new(0.0011, 0.0044),
    };

    /// <summary>
    /// Returns the estimated USD cost for the call, or 0 when the model is unknown.
    /// </summary>
    public static double Estimate(string? model, int inputTokens, int outputTokens)
    {
        if (string.IsNullOrWhiteSpace(model) || (inputTokens == 0 && outputTokens == 0))
        {
            return 0d;
        }

        if (TryResolve(model, out var pricing))
        {
            return (inputTokens / 1000d) * pricing.InputPer1K
                 + (outputTokens / 1000d) * pricing.OutputPer1K;
        }

        return 0d;
    }

    private static bool TryResolve(string model, out Pricing pricing)
    {
        if (PricesPer1K.TryGetValue(model, out pricing))
        {
            return true;
        }

        // OpenAI returns dated suffixes like "gpt-4o-2024-08-06" — strip the
        // trailing date so the catalog lookup still hits.
        var stripped = StripDatedSuffix(model);
        if (stripped is not null && PricesPer1K.TryGetValue(stripped, out pricing))
        {
            return true;
        }

        pricing = default;
        return false;
    }

    private static string? StripDatedSuffix(string model)
    {
        // Match `-yyyy-MM-dd` at the tail.
        if (model.Length < 11) return null;
        var tail = model.AsSpan(model.Length - 11);
        if (tail[0] != '-') return null;
        for (var i = 1; i < tail.Length; i++)
        {
            if (tail[i] == '-') continue;
            if (!char.IsDigit(tail[i])) return null;
        }
        return model[..^11];
    }
}
