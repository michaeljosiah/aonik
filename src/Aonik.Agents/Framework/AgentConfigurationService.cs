using System.Text.Json;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Manages persisted agent configurations with a two-level override model.
/// <para>
/// Resolution chain (for <see cref="GetResolvedAsync"/>):
/// <list type="number">
///   <item>Tenant-specific row (<c>TenantId == current</c>)</item>
///   <item>Global row (<c>TenantId == null</c>)</item>
///   <item><c>null</c> — caller falls back to code-based <see cref="IDomainAgentDescriptor"/></item>
/// </list>
/// </para>
/// </summary>
internal sealed class AgentConfigurationService : IAgentConfigurationService
{
    private readonly AgentsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAiModelResolver _modelResolver;
    private readonly IEnumerable<IDomainAgentDescriptor> _descriptors;
    private readonly ILogger<AgentConfigurationService> _logger;

    public AgentConfigurationService(
        AgentsDbContext dbContext,
        ITenantProvider tenantProvider,
        IAiModelResolver modelResolver,
        IEnumerable<IDomainAgentDescriptor> descriptors,
        ILogger<AgentConfigurationService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _modelResolver = modelResolver;
        _descriptors = descriptors;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AgentConfigurationResponse>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        _tenantProvider.TryGetCurrentTenantId(out var tenantId);

        // Fetch all global + tenant-specific rows visible to this tenant.
        // The nullable tenant query filter on AgentsDbContext already handles this
        // (shows rows where TenantId == null OR TenantId == currentTenant).
        var allRows = await _dbContext.Agents
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ThenBy(a => a.TenantId)
            .ToListAsync(cancellationToken);

        // Deduplicate: for each agent name, prefer the tenant-specific override
        // over the global default. This ensures the UI sees one entry per agent.
        var agents = allRows
            .GroupBy(a => a.Name)
            .Select(g => tenantId != Guid.Empty
                ? g.FirstOrDefault(a => a.TenantId == tenantId) ?? g.First()
                : g.FirstOrDefault(a => a.TenantId == null) ?? g.First())
            .OrderBy(a => a.Name)
            .ToList();

        // Batch-resolve model names for agents that have ModelId set
        var modelNames = await ResolveModelNamesAsync(
            agents.Where(a => a.ModelId.HasValue).Select(a => a.ModelId!.Value).Distinct(),
            cancellationToken);

        return agents.Select(a => MapToResponse(
            a, tenantId,
            a.ModelId.HasValue ? modelNames.GetValueOrDefault(a.ModelId.Value) : null)).ToList();
    }

    public async Task<AgentConfigurationResponse?> GetResolvedAsync(
        string agentName,
        CancellationToken cancellationToken = default)
    {
        _tenantProvider.TryGetCurrentTenantId(out var tenantId);

        // Fetch all rows matching this agent name (global + any tenant override).
        var rows = await _dbContext.Agents
            .AsNoTracking()
            .Where(a => a.Name == agentName)
            .ToListAsync(cancellationToken);

        // Resolution: prefer tenant-specific over global
        var resolved = tenantId != Guid.Empty
            ? rows.FirstOrDefault(a => a.TenantId == tenantId)
              ?? rows.FirstOrDefault(a => a.TenantId == null)
            : rows.FirstOrDefault(a => a.TenantId == null);

        if (resolved is null)
            return null;

        string? modelName = null;
        if (resolved.ModelId.HasValue)
            modelName = await _modelResolver.ResolveModelNameByIdAsync(resolved.ModelId.Value, cancellationToken);

        return MapToResponse(resolved, tenantId, modelName);
    }

    public async Task<AgentConfigurationResponse> UpsertOverrideAsync(
        string agentName,
        UpsertAgentConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Load the global default to use as baseline for fields not provided
        var globalDefault = await _dbContext.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Name == agentName && a.TenantId == null, cancellationToken);

        // Also get code-based descriptor as ultimate fallback
        var descriptor = _descriptors.FirstOrDefault(d => d.Name == agentName);

        // Try to find existing tenant override
        var existing = await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.Name == agentName && a.TenantId == tenantId, cancellationToken);

        if (existing is not null)
        {
            // Update existing override
            existing.Description = request.Description ?? existing.Description;
            existing.InstructionsText = request.InstructionsText ?? existing.InstructionsText;
            existing.ToolsetIdsJson = request.ToolsetIdsJson ?? existing.ToolsetIdsJson;
            existing.PermissionsProfileJson = request.PermissionsProfileJson ?? existing.PermissionsProfileJson;
            existing.RiskTier = request.RiskTier ?? existing.RiskTier;
            existing.IsActive = request.IsActive ?? existing.IsActive;

            // ModelId: Guid.Empty clears the assignment; non-null sets it; null leaves unchanged
            if (request.ModelId.HasValue)
                existing.ModelId = request.ModelId.Value == Guid.Empty ? null : request.ModelId.Value;

            // IconUrl: empty string clears; non-null sets; null leaves unchanged
            if (request.IconUrl is not null)
                existing.IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl;
        }
        else
        {
            // Create new tenant override — populate from global default, then apply request
            var resolvedModelId = request.ModelId.HasValue && request.ModelId.Value != Guid.Empty
                ? request.ModelId.Value
                : globalDefault?.ModelId;

            existing = new Agent
            {
                TenantId = tenantId,
                Name = agentName,
                Domain = globalDefault?.Domain ?? "custom",
                Description = request.Description
                    ?? globalDefault?.Description
                    ?? descriptor?.Description
                    ?? string.Empty,
                InstructionsText = request.InstructionsText
                    ?? globalDefault?.InstructionsText
                    ?? string.Empty,
                ToolsetIdsJson = request.ToolsetIdsJson
                    ?? globalDefault?.ToolsetIdsJson
                    ?? string.Empty,
                PermissionsProfileJson = request.PermissionsProfileJson
                    ?? globalDefault?.PermissionsProfileJson
                    ?? string.Empty,
                RiskTier = request.RiskTier
                    ?? globalDefault?.RiskTier
                    ?? "low",
                IsActive = request.IsActive
                    ?? globalDefault?.IsActive
                    ?? true,
                ModelId = resolvedModelId,
                IconUrl = request.IconUrl is not null && !string.IsNullOrWhiteSpace(request.IconUrl)
                    ? request.IconUrl
                    : globalDefault?.IconUrl
            };

            _dbContext.Agents.Add(existing);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Upserted agent configuration override for '{AgentName}' in tenant {TenantId}",
            agentName, tenantId);

        string? modelName = null;
        if (existing.ModelId.HasValue)
            modelName = await _modelResolver.ResolveModelNameByIdAsync(existing.ModelId.Value, cancellationToken);

        return MapToResponse(existing, tenantId, modelName);
    }

    public async Task DeleteOverrideAsync(
        string agentName,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var existing = await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.Name == agentName && a.TenantId == tenantId, cancellationToken);

        if (existing is null)
        {
            _logger.LogWarning(
                "No tenant override found for agent '{AgentName}' in tenant {TenantId}",
                agentName, tenantId);
            return;
        }

        _dbContext.Agents.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted agent configuration override for '{AgentName}' in tenant {TenantId}",
            agentName, tenantId);
    }

    public async Task SeedGlobalDefaultsAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        foreach (var descriptor in _descriptors)
        {
            var existing = await _dbContext.Agents
                .FirstOrDefaultAsync(a => a.Name == descriptor.Name && a.TenantId == null, cancellationToken);

            if (existing is not null)
            {
                // Update default icon if the resolved icon has changed
                var resolvedIcon = ResolveDefaultIconUrl(descriptor.Name);
                if (resolvedIcon is not null && existing.IconUrl != resolvedIcon)
                {
                    existing.IconUrl = resolvedIcon;
                }
                continue;
            }

            // Resolve all tool names from the descriptor
            var toolNames = descriptor.GetToolNames(serviceProvider);
            var toolsetJson = toolNames.Count > 0
                ? JsonSerializer.Serialize(toolNames)
                : "[]";

            // Determine risk tier: agents with mutating tools (those starting with
            // create/archive/cancel/mark/issue/capture patterns) get "medium"
            var hasMutatingTools = toolNames.Any(IsMutatingToolName);

            var agent = new Agent
            {
                TenantId = null,
                Name = descriptor.Name,
                Domain = ResolveDomain(descriptor.Name),
                Description = descriptor.Description,
                InstructionsText = descriptor.Instructions ?? string.Empty,
                ToolsetIdsJson = toolsetJson,
                RiskTier = hasMutatingTools ? "medium" : "low",
                IsActive = true,
                AgentType = AgentType.SubAgent,
                IconUrl = ResolveDefaultIconUrl(descriptor.Name),
            };

            _dbContext.Agents.Add(agent);

            _logger.LogInformation(
                "Seeded global default agent configuration for '{AgentName}' with {ToolCount} tools",
                descriptor.Name, toolNames.Count);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Batch-resolves model names for a set of model IDs using the AI model resolver.
    /// Returns a dictionary mapping model ID → model name for all IDs that resolved successfully.
    /// </summary>
    private async Task<Dictionary<Guid, string>> ResolveModelNamesAsync(
        IEnumerable<Guid> modelIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, string>();

        foreach (var modelId in modelIds)
        {
            try
            {
                var name = await _modelResolver.ResolveModelNameByIdAsync(modelId, cancellationToken);
                if (name is not null)
                    result[modelId] = name;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve model name for ModelId {ModelId}", modelId);
            }
        }

        return result;
    }

    /// <summary>
    /// Heuristic: a tool is considered mutating if its name contains a known
    /// mutation verb segment. Matches patterns like pf_create_*, finance_cancel_*, etc.
    /// </summary>
    private static bool IsMutatingToolName(string toolName)
    {
        var mutationVerbs = new[] { "_create_", "_archive_", "_cancel_", "_issue_", "_mark_", "_capture_" };
        return mutationVerbs.Any(verb => toolName.Contains(verb, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns a placeholder icon path for system agents. These are served as static
    /// files from the Admin UI public directory and should be replaced with final artwork.
    /// </summary>
    private static string? ResolveDefaultIconUrl(string agentName)
    {
        return agentName switch
        {
            "personal-finance-agent" => "/images/agents/agent-1.png",
            "finance-agent" => "/images/agents/agent-2.png",
            "financial-life-graph-agent" => "/images/agents/agent-3.png",
            "platform-agent" => "/images/agents/agent-4.png",
            _ => null
        };
    }

    private static string ResolveDomain(string agentName)
    {
        return agentName switch
        {
            "finance-agent" => "finance",
            "financial-life-graph-agent" => "finance",
            "personal-finance-agent" => "personal-finance",
            "platform-agent" => "platform",
            _ => "custom"
        };
    }

    private AgentConfigurationResponse MapToResponse(Agent agent, Guid tenantId, string? modelName = null)
    {
        // Look up the code-based descriptor to surface RequiresUserBrief
        var descriptor = _descriptors.FirstOrDefault(
            d => string.Equals(d.Name, agent.Name, StringComparison.OrdinalIgnoreCase));

        return new AgentConfigurationResponse
        {
            Id = agent.Id,
            Name = agent.Name,
            Domain = agent.Domain,
            Description = agent.Description,
            InstructionsText = agent.InstructionsText,
            ToolsetIdsJson = agent.ToolsetIdsJson,
            PermissionsProfileJson = agent.PermissionsProfileJson,
            RiskTier = agent.RiskTier,
            IsActive = agent.IsActive,
            TenantId = agent.TenantId,
            ModelId = agent.ModelId,
            ModelName = modelName,
            IconUrl = agent.IconUrl,
            RequiresUserBrief = descriptor?.RequiresUserBrief ?? false,
            IsOverride = agent.TenantId is not null && agent.TenantId != Guid.Empty,
            AgentType = agent.AgentType,
            CreatedAt = agent.CreatedAt,
            UpdatedAt = agent.UpdatedAt
        };
    }
}
