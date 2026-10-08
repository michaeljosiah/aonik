using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aonik.Commerce.Contracts.Models.Catalog;

/// <summary>Accept one named allergen per entry; the standard enum converter combines comma-separated names.</summary>
public sealed class RegulatedAllergenJsonConverter : JsonConverter<RegulatedAllergen>
{
    public override RegulatedAllergen Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var name = reader.GetString();
            if (Enum.TryParse<RegulatedAllergen>(name, ignoreCase: true, out var allergen)
                && Enum.IsDefined(allergen)
                && string.Equals(name, allergen.ToString(), StringComparison.OrdinalIgnoreCase))
                return allergen;
        }

        throw new JsonException("Each allergen must be one of the 14 regulated allergen names.");
    }

    public override void Write(Utf8JsonWriter writer, RegulatedAllergen value, JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value)) throw new JsonException("Unknown regulated allergen.");
        writer.WriteStringValue(value.ToString());
    }
}
