using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Schema;
using System.Text.RegularExpressions;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.SharedKernel.Serialization;

namespace Aonik.Agents.Services.Workflows;

internal static class WorkflowDefinitionCodec
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 32,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static WorkflowDefinition FromGraph(WorkflowGraphResponse graph)
    {
        Dictionary<Guid, string> keys = graph.Nodes.ToDictionary(node => node.Id, NodeKey);
        return new(graph.SchemaVersion, graph.DefinitionKind, graph.Slug, graph.Name, graph.Description,
            graph.Nodes.OrderBy(NodeKey, StringComparer.Ordinal)
                .Select(node => new WorkflowDefinitionNode(NodeKey(node), node.Kind, node.Label,
                    JsonSerializer.Deserialize<JsonElement>(node.ParamsJson, JsonOptions))).ToList(),
            graph.Edges.Select(edge => new WorkflowDefinitionEdge(
                    EdgeKey(keys[edge.FromNodeId], keys[edge.ToNodeId], edge.FromIndex, edge.RouteKey),
                    keys[edge.FromNodeId], keys[edge.ToNodeId], edge.FromIndex, edge.RouteKey))
                .OrderBy(edge => edge.Key, StringComparer.Ordinal).ToList());
    }

    public static string NodeKey(WorkflowGraphNode node) => node.Key ?? node.Id.ToString("D");
    public static string EdgeKey(string from, string to, int fromIndex, string? route) => $"{from}/{fromIndex}/{route ?? ""}/{to}";
    public static bool IsKey(string? value) => value is { Length: > 0 and <= 128 }
        && Regex.IsMatch(value, "^[a-zA-Z0-9][a-zA-Z0-9_.-]*$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static string Serialize(WorkflowDefinition definition)
    {
        JsonElement element = JsonSerializer.SerializeToElement(definition, JsonOptions);
        return CanonicalJson.Serialize(element);
    }

    public static string Hash(string definitionJson) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(definitionJson)));

    public static JsonElement DecisionSchema() => JsonSerializer.SerializeToElement(
        JsonOptions.GetJsonSchemaAsNode(typeof(DecisionTable)));

    public static JsonElement ToolSchema() => JsonSerializer.SerializeToElement(
        JsonOptions.GetJsonSchemaAsNode(typeof(WorkflowToolParameters)));
    public static JsonElement WaitSchema() => JsonSerializer.SerializeToElement(
        JsonOptions.GetJsonSchemaAsNode(typeof(WorkflowWaitParameters)));
    public static JsonElement HumanSchema() => JsonSerializer.SerializeToElement(
        JsonOptions.GetJsonSchemaAsNode(typeof(WorkflowHumanParameters)));
    public static JsonElement ParametersSchema<T>() => JsonSerializer.SerializeToElement(JsonOptions.GetJsonSchemaAsNode(typeof(T)));

}
