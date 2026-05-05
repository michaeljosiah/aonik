using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
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
        using var activity = AiTelemetry.ActivitySource.StartActivity("aonik.agent.resolve_context", ActivityKind.Internal);
        activity?.SetTag("aonik.agent.name", agentId ?? "orchestrator");

        if (string.IsNullOrEmpty(agentId))
        {
            var orchestratorAgent = await _orchestrator.GetAgentAsync(cancellationToken);
            activity?.SetTag("aonik.user_brief.required", false);
            // Orchestrator manages its own model via MasterOrchestratorService,
            // so we deliberately leave ConfiguredModelName null here — the
            // AGUI endpoint must NOT stamp a model on orchestrator runs.
            return new AgentContextResolution(orchestratorAgent, UserBriefPreamble: null, "skipped", null, ConfiguredModelName: null);
        }

        var requiresUserBrief = _descriptorsByName.TryGetValue(agentId, out var knownDescriptor)
            && knownDescriptor.RequiresUserBrief;
        activity?.SetTag("aonik.user_brief.required", requiresUserBrief);

        var agentTask = _domainResolver.ResolveAsync(agentId, cancellationToken);
        var userBriefTask = requiresUserBrief
            ? BuildCachedUserBriefAsync(cancellationToken)
            : Task.FromResult(new UserBriefResolution(null, "skipped", null));

        var resolution = await agentTask;
        activity?.SetTag("aonik.agent.configured_model", resolution.ConfiguredModelName ?? "<global default>");

        if (!resolution.Descriptor.RequiresUserBrief)
        {
            activity?.SetTag("aonik.user_brief.cache_status", "skipped");
            return new AgentContextResolution(
                resolution.Agent,
                UserBriefPreamble: null,
                "skipped",
                null,
                resolution.ConfiguredModelName);
        }

        var brief = await userBriefTask;
        activity?.SetTag("aonik.user_brief.cache_status", brief.CacheStatus);
        if (brief.DurationMs.HasValue)
            activity?.SetTag("aonik.user_brief.duration_ms", brief.DurationMs.Value);

        return new AgentContextResolution(
            resolution.Agent,
            brief.Preamble,
            brief.CacheStatus,
            brief.DurationMs,
            resolution.ConfiguredModelName);
    }

    private async Task<UserBriefResolution> BuildCachedUserBriefAsync(CancellationToken cancellationToken)
    {
        using var activity = AiTelemetry.ActivitySource.StartActivity("aonik.user_brief.resolve", ActivityKind.Internal);

        if (_currentUserProvider is null
            || _tenantProvider is null
            || !_currentUserProvider.TryGetCurrentUserId(out var userId)
            || !_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            activity?.SetTag("aonik.user_brief.cache_status", "skipped");
            return new UserBriefResolution(null, "skipped", null);
        }

        activity?.SetTag("aonik.tenant_id", tenantId.ToString());
        activity?.SetTag("aonik.user_id", userId.ToString());

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
            activity?.SetTag("aonik.user_brief.cache_status", cacheMiss ? "miss" : "hit");
            activity?.SetTag("aonik.user_brief.duration_ms", stopwatch.ElapsedMilliseconds);
            return new UserBriefResolution(
                preamble,
                cacheMiss ? "miss" : "hit",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            AiTelemetry.MarkError(activity, ex);
            activity?.SetTag("aonik.user_brief.cache_status", "error");
            activity?.SetTag("aonik.user_brief.duration_ms", stopwatch.ElapsedMilliseconds);
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
        using var activity = AiTelemetry.ActivitySource.StartActivity("aonik.user_brief.build_message", ActivityKind.Internal);
        activity?.SetTag("aonik.tenant_id", tenantId.ToString());
        activity?.SetTag("aonik.user_id", userId.ToString());

        var brief = await _userBriefProjector.ProjectAsync(tenantId, userId, cancellationToken: cancellationToken);
        var briefJson = JsonSerializer.Serialize(brief, BriefJsonOptions);
        activity?.SetTag("aonik.user_brief.payload_size_chars", briefJson.Length);

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
