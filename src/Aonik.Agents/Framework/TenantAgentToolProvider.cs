using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// The composite <see cref="ITenantAgentToolProvider"/> a domain agent descriptor consumes (Spec
/// 033 §8.6). It gathers the current tenant's remote MCP tools (§8.3) and declarative HTTP tools
/// (§8.4) as raw <see cref="AITool"/>s — each provider having already registered its classification
/// in the request-scoped store — and returns them for the descriptor to concatenate into its single
/// <c>IToolApprovalGate.GateAll(...)</c> call. There is no second gating path: tenant tools ride the
/// one that already exists and fails closed.
/// <para>
/// <see cref="GetTools"/> is synchronous to fit the descriptor's <c>Build</c> seam; the remote MCP
/// discovery it awaits is cached per (tenant, server, credential version), so only the first build
/// for a server blocks. ASP.NET Core has no synchronization context, so blocking here cannot
/// deadlock. Any failure degrades to "no tenant tools" rather than breaking agent build.
/// </para>
/// </summary>
internal sealed class TenantAgentToolProvider : ITenantAgentToolProvider
{
    private readonly ITenantMcpToolProvider _mcp;
    private readonly ITenantHttpToolFactory _http;
    private readonly ILogger<TenantAgentToolProvider> _logger;

    public TenantAgentToolProvider(
        ITenantMcpToolProvider mcp,
        ITenantHttpToolFactory http,
        ILogger<TenantAgentToolProvider> logger)
    {
        _mcp = mcp;
        _http = http;
        _logger = logger;
    }

    public IReadOnlyList<AITool> GetTools(IServiceProvider serviceProvider)
    {
        try
        {
            return GetToolsAsync(serviceProvider).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build tenant agent tools; continuing with built-ins only.");
            return [];
        }
    }

    private async Task<IReadOnlyList<AITool>> GetToolsAsync(IServiceProvider serviceProvider)
    {
        var mcpTools = await _mcp.GetToolsAsync(serviceProvider).ConfigureAwait(false);
        var httpTools = await _http.CreateToolsAsync(serviceProvider).ConfigureAwait(false);

        if (mcpTools.Count == 0)
        {
            return httpTools;
        }
        if (httpTools.Count == 0)
        {
            return mcpTools;
        }

        var combined = new List<AITool>(mcpTools.Count + httpTools.Count);
        combined.AddRange(mcpTools);
        combined.AddRange(httpTools);
        return combined;
    }
}
