using System.Text.Json;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
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
    private readonly IEnumerable<IDomainAgentDescriptor> _descriptors;
    private readonly ILogger<AgentConfigurationService> _logger;

    public AgentConfigurationService(
        AgentsDbContext dbContext,
        ITenantProvider tenantProvider,
        IEnumerable<IDomainAgentDescriptor> descriptors,
        ILogger<AgentConfigurationService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
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
        var agents = await _dbContext.Agents
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ThenBy(a => a.TenantId)
            .ToListAsync(cancellationToken);

        return agents.Select(a => MapToResponse(a, tenantId)).ToList();
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

        return resolved is not null
            ? MapToResponse(resolved, tenantId)
            : null;
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
        }
        else
        {
            // Create new tenant override — populate from global default, then apply request
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
                    ?? true
            };

            _dbContext.Agents.Add(existing);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Upserted agent configuration override for '{AgentName}' in tenant {TenantId}",
            agentName, tenantId);

        return MapToResponse(existing, tenantId);
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
            var exists = await _dbContext.Agents
                .AnyAsync(a => a.Name == descriptor.Name && a.TenantId == null, cancellationToken);

            if (exists)
                continue;

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
                IsActive = true
            };

            _dbContext.Agents.Add(agent);

            _logger.LogInformation(
                "Seeded global default agent configuration for '{AgentName}' with {ToolCount} tools",
                descriptor.Name, toolNames.Count);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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

    private static AgentConfigurationResponse MapToResponse(Agent agent, Guid tenantId)
    {
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
            IsOverride = agent.TenantId is not null && agent.TenantId != Guid.Empty,
            CreatedAt = agent.CreatedAt,
            UpdatedAt = agent.UpdatedAt
        };
    }
}
