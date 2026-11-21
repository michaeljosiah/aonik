using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Aonik.SharedKernel.Abstractions.Connectors;

public static class ConnectorJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 24,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };
    public static T Read<T>(JsonElement value) => value.Deserialize<T>(Options) ?? throw new ArgumentException("Connector arguments are required.");
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options) ?? throw new ArgumentException("Connector data is required.");
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
    public static JsonElement Schema<T>() => JsonSerializer.SerializeToElement(Options.GetJsonSchemaAsNode(typeof(T)));
}
