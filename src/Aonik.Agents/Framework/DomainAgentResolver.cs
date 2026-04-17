using System.Text.Json;
using Aonik.Agents.Contracts.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Scoped <see cref="IDomainAgentResolver"/> implementation. Memoises the
/// resolved (agent, descriptor) tuple per agent name within the scope so
/// repeated lookups in the same request skip the config DB read, tool
/// materialisation, and <c>ChatClientAgent</c> allocation.
/// </summary>
/// <remarks>
/// ASP.NET Core processes a single request linearly, so an unlocked
/// <see cref="Dictionary{TKey,TValue}"/> is sufficient. Swap for a
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>
/// plus <see cref="Lazy{T}"/> if a caller later resolves from parallel tasks.
/// </remarks>
internal sealed class DomainAgentResolver : IDomainAgentResolver
{
    private readonly IEnumerable<IDomainAgentDescriptor> _descriptors;
    private readonly IAgentConfigurationService _configService;
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _services;
    private readonly ILogger<DomainAgentResolver> _logger;

    private readonly Dictionary<string, (AIAgent Agent, IDomainAgentDescriptor Descriptor)> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public DomainAgentResolver(
        IEnumerable<IDomainAgentDescriptor> descriptors,
        IAgentConfigurationService configService,
        IChatClient chatClient,
        IServiceProvider services,
        ILogger<DomainAgentResolver> logger)
    {
        _descriptors = descriptors;
        _configService = configService;
        _chatClient = chatClient;
        _services = services;
        _logger = logger;
    }

    public async Task<(AIAgent Agent, IDomainAgentDescriptor Descriptor)> ResolveAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(agentId, out var cached))
            return cached;

        var descriptor = _descriptors.FirstOrDefault(
            d => string.Equals(d.Name, agentId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No domain agent descriptor registered with name '{agentId}'. " +
                $"Available: {string.Join(", ", _descriptors.Select(d => d.Name))}");

        var config = await _configService.GetResolvedAsync(agentId, cancellationToken);

        if (config is { IsActive: false })
            throw new InvalidOperationException($"Agent '{agentId}' is inactive per configuration.");

        AIAgent agent;
        if (config is not null)
        {
            var instructionsOverride = !string.IsNullOrWhiteSpace(config.InstructionsText)
                ? config.InstructionsText
                : null;

            HashSet<string>? allowedToolNames = null;
            if (!string.IsNullOrWhiteSpace(config.ToolsetIdsJson) && config.ToolsetIdsJson != "[]")
            {
                try
                {
                    var toolNames = JsonSerializer.Deserialize<List<string>>(config.ToolsetIdsJson);
                    if (toolNames is { Count: > 0 })
                        allowedToolNames = new HashSet<string>(toolNames, StringComparer.Ordinal);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Invalid ToolsetIdsJson for agent '{AgentName}' — using all tools", agentId);
                }
            }

            agent = descriptor.Build(_chatClient, _services, instructionsOverride, allowedToolNames);
            _logger.LogDebug("Resolved domain agent '{AgentName}' with config override", agentId);
        }
        else
        {
            agent = descriptor.Build(_chatClient, _services);
            _logger.LogDebug("Resolved domain agent '{AgentName}' with code defaults", agentId);
        }

        var result = (agent, descriptor);
        _cache[agentId] = result;
        return result;
    }
}
