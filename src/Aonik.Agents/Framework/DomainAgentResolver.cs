using System.Text.Json;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Agents;
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
    private readonly DescriptorModuleFilter _moduleFilter;
    private readonly ILogger<DomainAgentResolver> _logger;

    private readonly Dictionary<string, DomainAgentResolution> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public DomainAgentResolver(
        IEnumerable<IDomainAgentDescriptor> descriptors,
        IAgentConfigurationService configService,
        IChatClient chatClient,
        IServiceProvider services,
        DescriptorModuleFilter moduleFilter,
        ILogger<DomainAgentResolver> logger)
    {
        _descriptors = descriptors;
        _configService = configService;
        _chatClient = chatClient;
        _services = services;
        _moduleFilter = moduleFilter;
        _logger = logger;
    }

    public async Task<DomainAgentResolution> ResolveAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(agentId, out var cached))
            return cached;

        // Module gate (Spec 097 §12.1): a descriptor whose module is disabled for the tenant is
        // refused with ModuleDisabledException rather than reported as unknown.
        var descriptor = await _moduleFilter.FindAsync(_descriptors, agentId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"No domain agent descriptor registered with name '{agentId}'. " +
                $"Available: {string.Join(", ", (await _moduleFilter.FilterAsync(_descriptors, cancellationToken)).Select(d => d.Name))}");

        var config = await _configService.GetResolvedAsync(agentId, cancellationToken);

        if (config is { IsActive: false })
            throw new InvalidOperationException($"Agent '{agentId}' is inactive per configuration.");

        AIAgent agent;
        // Surface the configured model name (resolved from
        // AnkAgents.AiModelId by the configuration service) so the AGUI
        // / playground caller can stamp it onto ChatOptions.ModelId at
        // run time. Without this, the agent inherits the chat client's
        // global default and the per-agent override silently never
        // reaches the LLM — see the dev trace where personal-finance-
        // agent was configured for one model but always called
        // gpt-5-mini because of this gap.
        var configuredModelName = !string.IsNullOrWhiteSpace(config?.ModelName)
            ? config!.ModelName
            : null;

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
            _logger.LogDebug(
                "Resolved domain agent '{AgentName}' with config override; configured model: {ModelName}",
                agentId, configuredModelName ?? "<global default>");
        }
        else
        {
            agent = descriptor.Build(_chatClient, _services);
            _logger.LogDebug("Resolved domain agent '{AgentName}' with code defaults", agentId);
        }

        var result = new DomainAgentResolution(agent, descriptor, configuredModelName);
        _cache[agentId] = result;
        return result;
    }
}
