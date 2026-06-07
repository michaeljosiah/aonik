using System.Collections.Concurrent;
using Aonik.Agents.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Aonik.Agents.Framework;

/// <summary>
/// Connects to configured MCP servers via stdio transport and exposes their
/// tools as <see cref="AITool"/> instances. Manages MCP client lifecycles
/// and caches tool lists per server.
///
/// Configuration is read from "Agents:McpServers" section:
/// <code>
/// {
///   "Agents": {
///     "McpServers": [
///       {
///         "Name": "finance",
///         "Command": "dotnet",
///         "Arguments": ["run", "--project", "src/Aonik.Finance.Mcp"],
///         "Enabled": true
///       }
///     ]
///   }
/// }
/// </code>
/// </summary>
internal sealed class McpToolProvider : IMcpToolProvider
{
    private readonly IReadOnlyList<McpServerConfig> _servers;
    private readonly ILogger<McpToolProvider> _logger;
    private readonly string _parentEnvironmentName;
    private readonly bool _parentIsDevelopment;
    private readonly ConcurrentDictionary<string, McpClient> _clients = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<AITool>> _toolCache = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private bool _disposed;

    public McpToolProvider(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<McpToolProvider> logger)
    {
        _logger = logger;
        _parentEnvironmentName = hostEnvironment.EnvironmentName;
        _parentIsDevelopment = hostEnvironment.IsDevelopment();

        var servers = new List<McpServerConfig>();
        configuration.GetSection("Agents:McpServers").Bind(servers);
        _servers = servers.Where(s => s.Enabled).ToList();

        _logger.LogInformation(
            "McpToolProvider initialized with {Count} configured server(s): {Names}",
            _servers.Count,
            string.Join(", ", _servers.Select(s => s.Name)));
    }

    public IReadOnlyList<string> GetConfiguredServerNames()
        => _servers.Select(s => s.Name).ToList();

    public async Task<IReadOnlyList<AITool>> GetToolsAsync(
        string serverName,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_toolCache.TryGetValue(serverName, out var cached))
            return cached;

        var config = _servers.FirstOrDefault(
            s => s.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));

        if (config is null)
        {
            _logger.LogWarning("MCP server '{ServerName}' is not configured", serverName);
            return Array.Empty<AITool>();
        }

        return await ConnectAndGetToolsAsync(config, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<AITool>>> GetAllToolsAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new Dictionary<string, IReadOnlyList<AITool>>();

        foreach (var config in _servers)
        {
            var tools = await GetToolsAsync(config.Name, cancellationToken);
            result[config.Name] = tools;
        }

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var client in _clients.Values)
        {
            try
            {
                await client.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing MCP client");
            }
        }

        _clients.Clear();
        _toolCache.Clear();
        _connectLock.Dispose();
    }

    private async Task<IReadOnlyList<AITool>> ConnectAndGetToolsAsync(
        McpServerConfig config,
        CancellationToken cancellationToken)
    {
        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_toolCache.TryGetValue(config.Name, out var cached))
                return cached;

            _logger.LogInformation(
                "Connecting to MCP server '{ServerName}' via {Command} {Arguments}",
                config.Name, config.Command, string.Join(" ", config.Arguments));

            var transportOptions = new StdioClientTransportOptions
            {
                Name = config.Name,
                Command = config.Command,
                Arguments = config.Arguments
            };

            if (!string.IsNullOrEmpty(config.WorkingDirectory))
            {
                transportOptions.WorkingDirectory = config.WorkingDirectory;
            }

            foreach (var (key, value) in config.EnvironmentVariables)
            {
                // Defense-in-depth (finding C4): the first-party Finance/Platform MCP hosts wire
                // blanket-trust security stubs and refuse to start unless DOTNET_ENVIRONMENT=Development.
                // Never let a non-Development parent hand a spawned child a forged "Development"
                // environment, which would defeat that fail-closed guard from the outside.
                if (!_parentIsDevelopment
                    && (key.Equals("DOTNET_ENVIRONMENT", StringComparison.OrdinalIgnoreCase)
                        || key.Equals("ASPNETCORE_ENVIRONMENT", StringComparison.OrdinalIgnoreCase))
                    && string.Equals(value, "Development", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Ignoring '{Key}=Development' override for MCP server '{ServerName}': the parent " +
                        "host is running as '{ParentEnvironment}', and a spawned MCP child must not be " +
                        "forced into Development (it would bypass development-only host guards).",
                        key, config.Name, _parentEnvironmentName);
                    continue;
                }

                transportOptions.EnvironmentVariables ??= new Dictionary<string, string?>();
                transportOptions.EnvironmentVariables[key] = value;
            }

            var clientTransport = new StdioClientTransport(transportOptions);

            var client = await McpClient.CreateAsync(
                clientTransport,
                cancellationToken: cancellationToken);

            _clients[config.Name] = client;

            // McpClientTool inherits from AIFunction which extends AITool,
            // so these are directly usable as tools with IChatClient.
            var mcpTools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            var aiTools = mcpTools.Cast<AITool>().ToList().AsReadOnly();

            _toolCache[config.Name] = aiTools;

            _logger.LogInformation(
                "Connected to MCP server '{ServerName}' — {ToolCount} tool(s) available: {ToolNames}",
                config.Name,
                aiTools.Count,
                string.Join(", ", aiTools.Select(t => t.Name)));

            return aiTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to connect to MCP server '{ServerName}'",
                config.Name);

            return Array.Empty<AITool>();
        }
        finally
        {
            _connectLock.Release();
        }
    }
}
