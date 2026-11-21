using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aonik.SharedKernel.Serialization;

/// <summary>Stable object-key ordering for versioned snapshots and contract fingerprints. Array order and literal casing are preserved.</summary>
public static class CanonicalJson
{
    public static string Serialize(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string Hash(JsonElement value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(value))));

    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                Write(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (JsonElement child in value.EnumerateArray()) Write(writer, child);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}
