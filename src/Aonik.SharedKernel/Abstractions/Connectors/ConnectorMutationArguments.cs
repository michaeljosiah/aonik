using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.SharedKernel.Abstractions.Connectors;

public static class ConnectorMutationArguments
{
    public static void RequireReviewedAccount(ToolApprovalPreview? preview, Guid connectionId, string providerAccountId)
    {
        if (preview?.After is not { ValueKind: JsonValueKind.Object } after || !after.TryGetProperty("connectionId", out JsonElement connection)
            || !connection.TryGetGuid(out Guid reviewedConnection) || reviewedConnection != connectionId
            || !after.TryGetProperty("providerAccountId", out JsonElement provider) || provider.ValueKind != JsonValueKind.String || provider.GetString() != providerAccountId)
            throw new ConnectorAccessException("ReviewedAccountChanged", "The provider account no longer matches the exact reviewed operation.");
    }

    public static void RequireReviewedServiceAccount(ToolApprovalPreview? preview, ConnectorArtifactPin? artifact, int? credentialVersion)
    {
        if (artifact is null || credentialVersion is null || preview?.After is not { ValueKind: JsonValueKind.Object } after
            || !after.TryGetProperty("artifact", out JsonElement reviewedArtifact) || reviewedArtifact.ValueKind != JsonValueKind.Object
            || ConnectorJson.Read<ConnectorArtifactPin>(reviewedArtifact) != artifact
            || !after.TryGetProperty("serviceCredentialVersion", out JsonElement version) || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out int reviewedVersion) || reviewedVersion != credentialVersion)
            throw new ConnectorAccessException("ReviewedAccountChanged", "The installed release or service credential no longer matches the reviewed operation.");
    }
    public static Dictionary<string, object?> ToolArguments(Guid connectionId, JsonElement request)
        => new() { ["connectionId"] = connectionId, ["request"] = request };
    public static string Hash(Guid connectionId, JsonElement request) => ToolArgumentsHash.Compute(ToolArguments(connectionId, request));
    public static (Guid ConnectionId, JsonElement Request) ReadTool(string json, ConnectorActionRef action)
    {
        var (id, request) = ReadTool(json);
        return (id, GmailActionContracts.Normalize(action, request));
    }

    public static (Guid ConnectionId, JsonElement Request) ReadTool(string json)
    {
        JsonElement payload = ConnectorJson.Read<JsonElement>(json);
        if (payload.ValueKind != JsonValueKind.Object || payload.EnumerateObject().Count() != 2
            || !payload.TryGetProperty("connectionId", out JsonElement connection) || !connection.TryGetGuid(out Guid id) || id == Guid.Empty
            || !payload.TryGetProperty("request", out JsonElement request)) throw new ArgumentException("The durable connector tool binding is invalid.");
        return (id, request);
    }
}
