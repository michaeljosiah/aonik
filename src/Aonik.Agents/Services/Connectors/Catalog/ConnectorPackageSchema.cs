using System.Text;
using System.Text.Json;

using Json.Schema;

namespace Aonik.Agents.Services.Connectors.Catalog;

/// <summary>Reuses JSON Schema validation for passive, bounded, self-contained action contracts.</summary>
internal sealed class ConnectorPackageSchema
{
    private readonly JsonSchema _schema;

    public ConnectorPackageSchema(JsonElement declaration)
    {
        ValidateDeclaration(declaration);
        _schema = JsonSchema.FromText(declaration.GetRawText());
    }

    public void Require(JsonElement value, int maximumBytes)
    {
        if (value.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(value.GetRawText()) > maximumBytes)
            throw new ArgumentException("Connector arguments and results must be bounded typed objects.");
        RequireUnambiguous(value, 0);
        var options = new EvaluationOptions
        {
            EvaluateAs = SpecVersion.Draft202012, OutputFormat = OutputFormat.Flag,
            ValidateAgainstMetaSchema = true
        };
        options.SchemaRegistry.Fetch = _ => null;
        if (!_schema.Evaluate(value, options).IsValid) throw new ArgumentException("The value does not match the exact reviewed connector schema.");
    }

    internal static void ValidateDeclaration(JsonElement declaration)
    {
        int nodes = 0;
        Visit(declaration, 0);
        void Visit(JsonElement schema, int depth)
        {
            if (depth > 16 || ++nodes > 512) throw Unsupported();
            if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False) return;
            if (schema.ValueKind != JsonValueKind.Object) throw Unsupported();
            foreach (JsonProperty keyword in schema.EnumerateObject())
            {
                // Version one deliberately accepts inline schemas. This avoids network resolution,
                // reference cycles and unbounded regex evaluation inside a worker dispatch.
                if (keyword.Name is "$ref" or "$dynamicRef" or "$recursiveRef" or "pattern" or "patternProperties") throw Unsupported();
                if (keyword.Name == "$schema" && (keyword.Value.ValueKind != JsonValueKind.String
                    || keyword.Value.GetString() != "https://json-schema.org/draft/2020-12/schema")) throw Unsupported();
                if (keyword.Name is "properties" or "$defs" or "definitions" or "dependentSchemas")
                {
                    if (keyword.Value.ValueKind != JsonValueKind.Object) throw Unsupported();
                    foreach (JsonProperty property in keyword.Value.EnumerateObject()) Visit(property.Value, depth + 1);
                }
                else if (keyword.Name is "items" or "additionalProperties" or "not" or "if" or "then" or "else" or "contains"
                    or "propertyNames" or "unevaluatedProperties" or "unevaluatedItems" or "contentSchema") Visit(keyword.Value, depth + 1);
                else if (keyword.Name is "allOf" or "anyOf" or "oneOf" or "prefixItems")
                {
                    if (keyword.Value.ValueKind != JsonValueKind.Array) throw Unsupported();
                    foreach (JsonElement child in keyword.Value.EnumerateArray()) Visit(child, depth + 1);
                }
            }
        }
    }

    private static void RequireUnambiguous(JsonElement value, int depth)
    {
        if (depth > 16) throw new ArgumentException("Connector data exceeds the supported nesting depth.");
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw new ArgumentException("Duplicate connector fields are not supported.");
                RequireUnambiguous(property.Value, depth + 1);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (JsonElement child in value.EnumerateArray()) RequireUnambiguous(child, depth + 1);
    }

    private static InvalidOperationException Unsupported() => new("ConnectorPackage.ActionSchemaUnsupported");
}
