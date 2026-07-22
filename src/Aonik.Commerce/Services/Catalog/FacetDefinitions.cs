using System.Text.Json;

using Aonik.Commerce.Entities.Catalog;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>One parsed facet option (Spec 070 §5): a stable request <see cref="Value"/> token, a
/// mutable display <see cref="Label"/>, and — for Range groups — a half-open band [Min, Max),
/// min inclusive, max exclusive, null = open end.</summary>
public sealed record FacetOption(string Value, string Label, decimal? Min, decimal? Max);

/// <summary>
/// The single reader/validator for <see cref="FacetGroup.OptionsJson"/> — authoring validates with
/// it and matching consumes it, so the shape that passes validation is by construction the shape
/// matching understands.
/// </summary>
internal static class FacetDefinitions
{
    /// <summary>Strict parse for authoring (Spec 070 §11): throws
    /// <see cref="StorefrontValidationException"/> naming the problem. Value tokens must be
    /// non-empty and unique within the group; Range bands must be numeric, non-overlapping and
    /// ordered; non-Range options must not carry bounds.</summary>
    public static List<FacetOption> ParseStrict(string optionsJson, string matchKind)
    {
        List<FacetOption> options;
        try
        {
            options = ParseCore(optionsJson);
        }
        catch (JsonException)
        {
            throw new StorefrontValidationException("OptionsJson is not valid JSON.");
        }
        catch (FormatException ex)
        {
            throw new StorefrontValidationException(ex.Message);
        }

        if (options.Count == 0)
        {
            throw new StorefrontValidationException("A facet group requires at least one option.");
        }

        var duplicates = options
            .GroupBy(o => o.Value, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicates.Count > 0)
        {
            throw new StorefrontValidationException(
                $"Option value(s) {string.Join(", ", duplicates)} appear more than once; values are stable request tokens and must be unique.");
        }

        if (matchKind == FacetMatchKinds.Range)
        {
            ValidateBands(options);
        }
        else if (options.Any(o => o.Min is not null || o.Max is not null))
        {
            throw new StorefrontValidationException(
                $"min/max bounds are only valid on a {FacetMatchKinds.Range} group.");
        }

        return options;
    }

    /// <summary>Lenient parse for the read/matching side: a malformed stored definition yields an
    /// empty list (the group matches nothing and renders no options) rather than a 500 — the same
    /// defensive posture as product JSON (§11).</summary>
    public static IReadOnlyList<FacetOption> ParseLenient(string optionsJson)
    {
        try
        {
            return ParseCore(optionsJson);
        }
        catch (JsonException)
        {
            return [];
        }
        catch (FormatException)
        {
            return [];
        }
    }

    private static List<FacetOption> ParseCore(string optionsJson)
    {
        using var document = JsonDocument.Parse(optionsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("OptionsJson must be a JSON array of option objects.");
        }

        var options = new List<FacetOption>();
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("Every facet option must be a JSON object.");
            }

            var value = ReadRequiredString(entry, "value");
            var label = ReadRequiredString(entry, "label");

            options.Add(new FacetOption(value, label, ReadBound(entry, "min"), ReadBound(entry, "max")));
        }

        return options;
    }

    private static string ReadRequiredString(JsonElement entry, string property)
    {
        if (!entry.TryGetProperty(property, out var element)
            || element.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw new FormatException($"Every facet option requires a non-empty string '{property}'.");
        }
        return element.GetString()!.Trim();
    }

    private static decimal? ReadBound(JsonElement entry, string property)
    {
        if (!entry.TryGetProperty(property, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw new FormatException($"A range bound '{property}' must be a JSON number or null.");
        }
        return element.GetDecimal();
    }

    /// <summary>Bands must be ordered and non-overlapping. Because bands are half-open [min, max),
    /// adjacent bands sharing a boundary (…, 500) + [500, …) are legal and gapless.</summary>
    private static void ValidateBands(List<FacetOption> options)
    {
        foreach (var option in options)
        {
            if (option.Min is null && option.Max is null)
            {
                throw new StorefrontValidationException(
                    $"Range option '{option.Value}' must declare at least one of min/max.");
            }
            if (option.Min is { } min && option.Max is { } max && min >= max)
            {
                throw new StorefrontValidationException(
                    $"Range option '{option.Value}' has min >= max; bands are half-open [min, max).");
            }
        }

        var ordered = options
            .OrderBy(o => o.Min ?? decimal.MinValue)
            .ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            var previousMax = ordered[i - 1].Max ?? decimal.MaxValue;
            var currentMin = ordered[i].Min ?? decimal.MinValue;
            if (currentMin < previousMax)
            {
                throw new StorefrontValidationException(
                    $"Range options '{ordered[i - 1].Value}' and '{ordered[i].Value}' overlap; bands must be disjoint.");
            }
        }

        if (!ordered.SequenceEqual(options))
        {
            throw new StorefrontValidationException(
                "Range options must be declared in ascending band order — the storefront renders them verbatim.");
        }
    }
}
