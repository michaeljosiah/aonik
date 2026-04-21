using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Agents.Services;

public sealed class AgentContextualizer : IAgentContextualizer
{
    private static readonly JsonSerializerOptions BriefJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly FusionCacheEntryOptions UserBriefEntryOptions = new(TimeSpan.FromMinutes(1))
    {
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromMinutes(5),
    };

    private readonly IDomainAgentResolver _domainResolver;
    private readonly IReadOnlyDictionary<string, IDomainAgentDescriptor> _descriptorsByName;
    private readonly IMasterOrchestratorService _orchestrator;
    private readonly IUserBriefProjector _userBriefProjector;
    private readonly ICurrentUserProvider? _currentUserProvider;
    private readonly ITenantProvider? _tenantProvider;
    private readonly IFusionCache _cache;
    private readonly ILogger<AgentContextualizer> _logger;

    public AgentContextualizer(
        IDomainAgentResolver domainResolver,
        IEnumerable<IDomainAgentDescriptor> descriptors,
        IMasterOrchestratorService orchestrator,
        IUserBriefProjector userBriefProjector,
        IFusionCache cache,
        ILogger<AgentContextualizer> logger,
        ICurrentUserProvider? currentUserProvider = null,
        ITenantProvider? tenantProvider = null)
    {
        _domainResolver = domainResolver;
        _descriptorsByName = descriptors.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        _orchestrator = orchestrator;
        _userBriefProjector = userBriefProjector;
        _cache = cache;
        _currentUserProvider = currentUserProvider;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<AgentContextResolution> ResolveAsync(
        string? agentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(agentId))
        {
            var orchestratorAgent = await _orchestrator.GetAgentAsync(cancellationToken);
            return new AgentContextResolution(orchestratorAgent, UserBriefPreamble: null, "skipped", null);
        }

        var requiresUserBrief = _descriptorsByName.TryGetValue(agentId, out var knownDescriptor)
            && knownDescriptor.RequiresUserBrief;

        var agentTask = _domainResolver.ResolveAsync(agentId, cancellationToken);
        var userBriefTask = requiresUserBrief
            ? BuildCachedUserBriefAsync(cancellationToken)
            : Task.FromResult(new UserBriefResolution(null, "skipped", null));

        var (agent, descriptor) = await agentTask;

        if (!descriptor.RequiresUserBrief)
        {
            return new AgentContextResolution(agent, UserBriefPreamble: null, "skipped", null);
        }

        var brief = await userBriefTask;
        return new AgentContextResolution(agent, brief.Preamble, brief.CacheStatus, brief.DurationMs);
    }

    private async Task<UserBriefResolution> BuildCachedUserBriefAsync(CancellationToken cancellationToken)
    {
        if (_currentUserProvider is null
            || _tenantProvider is null
            || !_currentUserProvider.TryGetCurrentUserId(out var userId)
            || !_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            return new UserBriefResolution(null, "skipped", null);
        }

        var cacheMiss = false;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var preamble = await _cache.GetOrSetAsync(
                BuildUserBriefCacheKey(tenantId, userId),
                async ct =>
                {
                    cacheMiss = true;
                    return await TryBuildUserBriefMessageAsync(tenantId, userId, ct);
                },
                UserBriefEntryOptions,
                cancellationToken);

            stopwatch.Stop();
            return new UserBriefResolution(
                preamble,
                cacheMiss ? "miss" : "hit",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "Failed to build User Brief for user {UserId} in tenant {TenantId} — proceeding without brief",
                userId, tenantId);
            return new UserBriefResolution(null, "error", stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<ChatMessage?> TryBuildUserBriefMessageAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var brief = await _userBriefProjector.ProjectAsync(tenantId, userId, cancellationToken: cancellationToken);
        var briefJson = JsonSerializer.Serialize(brief, BriefJsonOptions);

        var content = $"""
            ## User Brief (current context — treat as ground truth for this session)

            ```json
            {briefJson}
            ```
            """;

        return new ChatMessage(ChatRole.System, content);
    }

    private static string BuildUserBriefCacheKey(Guid tenantId, Guid userId)
        => $"agui:user-brief:v1:{tenantId:N}:{userId:N}";

    private sealed record UserBriefResolution(
        ChatMessage? Preamble,
        string CacheStatus,
        long? DurationMs);
}
