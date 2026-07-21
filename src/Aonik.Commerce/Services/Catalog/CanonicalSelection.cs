using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>
/// Byte-stable serialization of an option selection (Spec 066 §7).
/// </summary>
/// <remarks>
/// Two equivalent selections must produce byte-identical JSON, because Spec 068 uses raw string
/// equality of this form as its cart line-merge key. Sorting the multi-select arrays alone is not
/// enough — two submissions can also present the group properties in different orders, and
/// serializers commonly preserve insertion order — so <em>object keys are sorted too</em>.
/// </remarks>
internal static class CanonicalSelection
{
    /// <summary>
    /// Serialize a resolved selection: group keys sorted ordinally, multi-select values sorted and
    /// de-duplicated, single-select values written as bare strings, no whitespace.
    /// </summary>
    public static string Serialize(IReadOnlyDictionary<string, IReadOnlyList<string>> selection, ISet<string> multiSelectGroups)
    {
        var json = new JsonObject();

        foreach (var groupKey in selection.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var values = selection[groupKey];

            if (multiSelectGroups.Contains(groupKey))
            {
                var array = new JsonArray();
                foreach (var value in values.Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal))
                {
                    array.Add(JsonValue.Create(value));
                }
                json[groupKey] = array;
            }
            else
            {
                json[groupKey] = JsonValue.Create(values.Count > 0 ? values[0] : string.Empty);
            }
        }

        return json.ToJsonString(SerializerOptions);
    }

    /// <summary>
    /// Read a selection payload into group → chosen keys, without judging it against any product.
    /// Shape errors (non-object root, nested objects, non-string array entries) throw; membership
    /// and mode validation belong to the caller, which knows the product's effective options.
    /// </summary>
    public static Dictionary<string, List<string>> Parse(JsonElement selection, string ruleIdForShape)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (selection.ValueKind == JsonValueKind.Null || selection.ValueKind == JsonValueKind.Undefined)
        {
            return result;
        }

        if (selection.ValueKind != JsonValueKind.Object)
        {
            throw new OptionValidationException(ruleIdForShape, "A selection must be a JSON object keyed by option group.");
        }

        foreach (var property in selection.EnumerateObject())
        {
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.String:
                    result[property.Name] = [property.Value.GetString() ?? string.Empty];
                    break;

                case JsonValueKind.Array:
                    var values = new List<string>();
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String)
                        {
                            throw new OptionValidationException(
                                ruleIdForShape,
                                $"Group '{property.Name}' has a non-string entry; option keys are strings.");
                        }
                        values.Add(item.GetString() ?? string.Empty);
                    }
                    result[property.Name] = values;
                    break;

                case JsonValueKind.Null:
                    // An explicit null means "no choice", which only matters for multi-select
                    // groups; recorded as empty so rule V4 can reject it with the right message.
                    result[property.Name] = [];
                    break;

                default:
                    throw new OptionValidationException(
                        ruleIdForShape,
                        $"Group '{property.Name}' must be a string or an array of strings.");
            }
        }

        return result;
    }

    /// <summary>Parse a stored canonical selection string. Malformed JSON is the one error that
    /// survives into the drift path — everything else is remapped and reported.</summary>
    public static Dictionary<string, List<string>> ParseStored(string canonicalSelectionJson)
    {
        if (string.IsNullOrWhiteSpace(canonicalSelectionJson))
        {
            return new Dictionary<string, List<string>>(StringComparer.Ordinal);
        }

        using var document = JsonDocument.Parse(canonicalSelectionJson);
        return Parse(document.RootElement, "V5");
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };
}
