using System.Text.Json;

namespace Aonik.Ai.Services;

/// <summary>
/// Pure static helper that computes estimated cost from token counts and
/// a <c>CostProfileJson</c> string (typically sourced from models.dev).
/// Expected JSON format: <c>{"input": &lt;price_per_million&gt;, "output": &lt;price_per_million&gt;}</c>
/// where prices are in USD per million tokens.
/// </summary>
public static class AiCostCalculator
{
    private const decimal PerMillionDivisor = 1_000_000m;

    /// <summary>
    /// Computes cost from token counts and a <c>CostProfileJson</c> string.
    /// Returns <c>0</c> if the profile is null, empty, <c>"{}"</c>, or malformed.
    /// </summary>
    public static decimal ComputeCost(long inputTokens, long outputTokens, string? costProfileJson)
    {
        if (string.IsNullOrWhiteSpace(costProfileJson)
            || costProfileJson.Trim() == "{}")
        {
            return 0m;
        }

        try
        {
            using var doc = JsonDocument.Parse(costProfileJson);
            var root = doc.RootElement;

            var inputRate = TryGetDecimal(root, "input");
            var outputRate = TryGetDecimal(root, "output");

            if (inputRate == 0m && outputRate == 0m)
            {
                return 0m;
            }

            return (inputTokens * inputRate / PerMillionDivisor)
                 + (outputTokens * outputRate / PerMillionDivisor);
        }
        catch (JsonException)
        {
            return 0m;
        }
    }

    private static decimal TryGetDecimal(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var element))
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.GetDecimal(),
                JsonValueKind.String when decimal.TryParse(
                    element.GetString(), out var parsed) => parsed,
                _ => 0m,
            };
        }

        return 0m;
    }
}
