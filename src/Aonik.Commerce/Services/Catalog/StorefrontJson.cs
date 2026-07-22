using System.Globalization;
using System.Text.Json;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>
/// Defensive JSON reads for the storefront browse path (Spec 070 §11): a malformed legacy row must
/// render with empty tags, drop out of facet matching, and log a warning — one bad row must never
/// 500 the public browse. Writes are validated strictly elsewhere; reads assume nothing.
/// </summary>
internal static class StorefrontJson
{
    /// <summary>Parses a JSON array of strings. Malformed input (bad JSON, non-array, non-string
    /// entries) yields an empty list and <paramref name="malformed"/> = true — the caller decides
    /// whether that is a logged warning (legacy rows) or a 400 (authoring).</summary>
    public static IReadOnlyList<string> ParseStringArray(string? json, out bool malformed)
    {
        malformed = false;

        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                malformed = true;
                return [];
            }

            var values = new List<string>();
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.String)
                {
                    malformed = true;
                    return [];
                }
                values.Add(entry.GetString() ?? string.Empty);
            }
            return values;
        }
        catch (JsonException)
        {
            malformed = true;
            return [];
        }
    }

    /// <summary>Parses a JSON object, returning a detached element. Malformed or non-object input
    /// yields null and <paramref name="malformed"/> = true.</summary>
    public static JsonElement? ParseObject(string? json, out bool malformed)
    {
        malformed = false;

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                malformed = true;
                return null;
            }
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            malformed = true;
            return null;
        }
    }

    /// <summary>Reads the string value at a dot path ("spice", "nutrition.kcal"). Numbers and
    /// booleans read as their invariant string form so an attribute authored as
    /// <c>"spice": 2</c> still compares. Null when the path is absent or non-scalar.</summary>
    public static string? ReadString(JsonElement? root, string path)
    {
        var element = Traverse(root, path);
        return element?.ValueKind switch
        {
            JsonValueKind.String => element.Value.GetString(),
            JsonValueKind.Number => element.Value.GetDecimal().ToString(CultureInfo.InvariantCulture),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    /// <summary>Reads the numeric value at a dot path. A JSON number reads directly; a numeric
    /// string parses invariantly (defensive — attribute authoring predates validation). Null when
    /// absent or non-numeric: a product missing the value matches no range band (Spec 070 §6).</summary>
    public static decimal? ReadNumber(JsonElement? root, string path)
    {
        var element = Traverse(root, path);
        return element?.ValueKind switch
        {
            JsonValueKind.Number => element.Value.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(
                element.Value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    private static JsonElement? Traverse(JsonElement? root, string path)
    {
        if (root is not { } current)
        {
            return null;
        }

        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
            {
                return null;
            }
            current = next;
        }

        return current;
    }
}
