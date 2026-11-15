using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Serialization;

namespace Aonik.Agents.Services.Connectors.Catalog;

/// <summary>Remote HTTP MCP only. A signed contract is checked against current server metadata before review and use.</summary>
internal sealed class ConnectorPackageMcpClient(ITenantCredentialProtector protector, ITenantEgressAllowList egress, IHttpClientFactory httpClients)
{
    public async Task<McpClient> ConnectAsync(TenantMcpServer server, VerifiedConnectorPackage package, CancellationToken cancellationToken = default)
    {
        if (!egress.IsAllowed(server.Endpoint, out _)) throw new ConnectorAccessException("EndpointBlocked", "The reviewed endpoint is no longer allowed.");
        var headers = TenantRemoteAuth.RequireHeaders(server.AuthKind, server.ProtectedAuthJson, protector, package.Manifest.Authentication.HeaderName);
        var options = new HttpClientTransportOptions
        {
            Name = server.Name, Endpoint = new Uri(server.Endpoint), AdditionalHeaders = headers,
            TransportMode = HttpTransportMode.StreamableHttp, MaxReconnectionAttempts = 0
        };
        var transport = new HttpClientTransport(options, httpClients.CreateClient("CuratedConnectorTool"), NullLoggerFactory.Instance, ownsHttpClient: true);
        McpClient? client = null;
        try
        {
            client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            await RequireContractsAsync(client, package, cancellationToken);
            return client;
        }
        catch (Exception error)
        {
            if (client is not null) await client.DisposeAsync(); else await transport.DisposeAsync();
            if (error is McpException or HttpRequestException or IOException) throw new ConnectorProviderException("RemoteMcpDiscoveryFailed");
            throw;
        }
    }

    private static async Task RequireContractsAsync(McpClient client, VerifiedConnectorPackage package, CancellationToken cancellationToken)
    {
        var discovered = new List<Tool>();
        string? cursor = null;
        var cursors = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            ListToolsResult page = await client.ListToolsAsync(new ListToolsRequestParams { Cursor = cursor }, cancellationToken);
            discovered.AddRange(page.Tools);
            if (discovered.Count > 100) throw Changed();
            cursor = page.NextCursor;
            if (cursor is not null && (!cursors.Add(cursor) || cursors.Count >= 10)) throw Changed();
        } while (cursor is not null);
        if (discovered.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count() != discovered.Count) throw Changed();
        foreach (ConnectorPackageAction action in package.Manifest.Actions)
        {
            Tool tool = discovered.SingleOrDefault(tool => tool.Name == action.RemoteToolName) ?? throw Changed();
            if (CanonicalJson.Hash(tool.InputSchema) != CanonicalJson.Hash(action.InputSchema)
                || tool.OutputSchema is not { } output || CanonicalJson.Hash(output) != CanonicalJson.Hash(action.OutputSchema)) throw Changed();
        }
    }

    private static ConnectorAccessException Changed() => new("ConnectorContractChanged", "The remote MCP tool schemas do not match the signed release. A new reviewed release is required.");
}
