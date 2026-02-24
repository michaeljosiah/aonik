namespace Aonik.Agents.Framework;

/// <summary>
/// Configuration for an MCP server that can be connected to via stdio transport.
/// Bound from the "Agents:McpServers" configuration section.
/// </summary>
public sealed class McpServerConfig
{
    /// <summary>
    /// Logical name for this MCP server (e.g. "finance", "platform").
    /// Used as the key when requesting tools from <see cref="Contracts.Services.IMcpToolProvider"/>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The command to launch the MCP server process (e.g. "dotnet").
    /// </summary>
    public string Command { get; set; } = "dotnet";

    /// <summary>
    /// Arguments passed to the command (e.g. "run --project src/Aonik.Finance.Mcp").
    /// </summary>
    public List<string> Arguments { get; set; } = new();

    /// <summary>
    /// Optional working directory for the MCP server process.
    /// If not set, the current process working directory is used.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Optional environment variables to set for the MCP server process.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Whether this server is enabled. Allows disabling servers in configuration
    /// without removing them.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
