using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Services;

public sealed class AgentContextualizer : IAgentContextualizer
{
    private static readonly JsonSerializerOptions BriefJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IDomainAgentResolver _domainResolver;
    private readonly IMasterOrchestratorService _orchestrator;
    private readonly IUserBriefProjector _userBriefProjector;
    private readonly ICurrentUserProvider? _currentUserProvider;
    private readonly ITenantProvider? _tenantProvider;
    private readonly ILogger<AgentContextualizer> _logger;

    public AgentContextualizer(
        IDomainAgentResolver domainResolver,
        IMasterOrchestratorService orchestrator,
        IUserBriefProjector userBriefProjector,
        ILogger<AgentContextualizer> logger,
        ICurrentUserProvider? currentUserProvider = null,
        ITenantProvider? tenantProvider = null)
    {
        _domainResolver = domainResolver;
        _orchestrator = orchestrator;
        _userBriefProjector = userBriefProjector;
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
            return new AgentContextResolution(orchestratorAgent, UserBriefPreamble: null);
        }

        var (agent, descriptor) = await _domainResolver.ResolveAsync(agentId, cancellationToken);

        if (!descriptor.RequiresUserBrief)
        {
            return new AgentContextResolution(agent, UserBriefPreamble: null);
        }

        var preamble = await TryBuildUserBriefMessageAsync(cancellationToken);
        return new AgentContextResolution(agent, preamble);
    }

    private async Task<ChatMessage?> TryBuildUserBriefMessageAsync(CancellationToken cancellationToken)
    {
        if (_currentUserProvider is null
            || _tenantProvider is null
            || !_currentUserProvider.TryGetCurrentUserId(out var userId)
            || !_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            return null;
        }

        try
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
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to build User Brief for user {UserId} in tenant {TenantId} — proceeding without brief",
                userId, tenantId);
            return null;
        }
    }
}
