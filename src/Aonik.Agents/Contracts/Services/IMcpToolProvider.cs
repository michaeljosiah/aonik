using Microsoft.Extensions.AI;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Provides tools from external MCP (Model Context Protocol) servers.
/// Each MCP server exposes domain-specific tools that agents can invoke.
/// The provider manages connections to configured MCP servers and returns
/// their tools as <see cref="AITool"/> instances compatible with MAF agents.
/// </summary>
public interface IMcpToolProvider : IAsyncDisposable
{
    /// <summary>
    /// Gets all tools from a specific named MCP server.
    /// </summary>
    /// <param name="serverName">
    /// The configured MCP server name (e.g. "finance", "platform").
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A list of <see cref="AITool"/> instances exposed by the server,
    /// or an empty list if the server is not configured or unreachable.
    /// </returns>
    Task<IReadOnlyList<AITool>> GetToolsAsync(
        string serverName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tools from all configured MCP servers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A dictionary keyed by server name, with values being the tools
    /// exposed by each server.
    /// </returns>
    Task<IReadOnlyDictionary<string, IReadOnlyList<AITool>>> GetAllToolsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the names of all configured MCP servers.
    /// </summary>
    IReadOnlyList<string> GetConfiguredServerNames();
}
