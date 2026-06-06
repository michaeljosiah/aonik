using System.Text.Json;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Aonik.Agents.Framework;

/// <summary>
/// Resolves the current tenant's active, approved remote MCP servers (Spec 033 §8.3) into raw
/// <see cref="AITool"/> instances, registering each tool's classification in the request-scoped
/// <see cref="ITenantToolClassificationStore"/> so the descriptor's single <c>GateAll</c> pass wraps
/// them like built-ins. Remote-only (<see cref="HttpClientTransport"/>) — no local process spawn;
/// egress is re-checked at connect; connections are cached per credential version and never shared
/// across tenants.
/// </summary>
internal interface ITenantMcpToolProvider
{
    Task<IReadOnlyList<AITool>> GetToolsAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}

internal sealed class TenantMcpToolProvider : ITenantMcpToolProvider
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(20);

    private readonly TenantMcpConnectionCache _cache;
    private readonly ITenantEgressAllowList _egress;
    private readonly ILogger<TenantMcpToolProvider> _logger;

    public TenantMcpToolProvider(
        TenantMcpConnectionCache cache,
        ITenantEgressAllowList egress,
        ILogger<TenantMcpToolProvider> logger)
    {
        _cache = cache;
        _egress = egress;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AITool>> GetToolsAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var tenantProvider = serviceProvider.GetService<ITenantProvider>();
        if (tenantProvider is null || !tenantProvider.TryGetCurrentTenantId(out var tenantId) || tenantId == Guid.Empty)
        {
            return [];
        }

        var db = serviceProvider.GetRequiredService<AgentsDbContext>();
        var protector = serviceProvider.GetRequiredService<ITenantCredentialProtector>();
        var store = serviceProvider.GetService<ITenantToolClassificationStore>();

        // Tenant query filter scopes this to the current tenant automatically.
        var servers = await db.TenantMcpServers
            .AsNoTracking()
            .Where(s => s.IsActive && s.ApprovalState == TenantExtensionApprovalState.Approved)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (servers.Count == 0)
        {
            return [];
        }

        var tools = new List<AITool>();
        foreach (var server in servers)
        {
            // Re-check egress at connect time — the allow-list may have changed since approval.
            if (!_egress.IsAllowed(server.Endpoint, out var reason))
            {
                _logger.LogWarning("Skipping tenant MCP server {Server}: {Reason}", server.Name, reason);
                continue;
            }

            CachedMcpConnection connection;
            try
            {
                var cacheKey = TenantMcpConnectionCache.Key(tenantId, server.Id, server.CredentialVersion);
                connection = await _cache
                    .GetOrConnectAsync(cacheKey, () => ConnectAsync(server, protector))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to tenant MCP server {Server}", server.Name);
                continue;
            }

            var prefixes = ParsePrefixes(server.AllowedToolPrefixesJson);
            foreach (var tool in connection.Tools)
            {
                if (prefixes.Count > 0 && !prefixes.Any(p => tool.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                store?.Register(tool.Name, TenantToolRiskMapping.ClassifyMcpTool(tool.Name, server));
                tools.Add(tool);
            }
        }

        return tools;
    }

    private static async Task<CachedMcpConnection> ConnectAsync(TenantMcpServer server, ITenantCredentialProtector protector)
    {
        var headers = TenantRemoteAuth.BuildHeaders(server.AuthKind, server.ProtectedAuthJson, protector);
        var options = new HttpClientTransportOptions
        {
            Name = server.Name,
            Endpoint = new Uri(server.Endpoint),
        };
        if (headers.Count > 0)
        {
            options.AdditionalHeaders = headers;
        }

        var transport = new HttpClientTransport(options);
        using var cts = new CancellationTokenSource(ConnectTimeout);
        var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token).ConfigureAwait(false);
        var discovered = await client.ListToolsAsync(cancellationToken: cts.Token).ConfigureAwait(false);
        var tools = discovered.Cast<AITool>().ToList();
        return new CachedMcpConnection { Client = client, Tools = tools };
    }

    private static List<string> ParsePrefixes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }
}
