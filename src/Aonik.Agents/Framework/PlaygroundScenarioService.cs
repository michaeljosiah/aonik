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
/// Manages playground scenarios — CRUD operations with tenant isolation.
/// Scenarios are always tenant-scoped (no global/nullable pattern).
/// </summary>
internal sealed class PlaygroundScenarioService : IPlaygroundScenarioService
{
    private readonly AgentsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<PlaygroundScenarioService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public PlaygroundScenarioService(
        AgentsDbContext dbContext,
        ITenantProvider tenantProvider,
        ILogger<PlaygroundScenarioService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlaygroundScenarioSummaryResponse>> ListAsync(
        string? agentName = null,
        string? tag = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PlaygroundScenarios
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(agentName))
            query = query.Where(s => s.AgentName == agentName);

        // Tag filtering: check if the JSON array contains the tag string.
        // This uses a simple Contains check which works for exact tag matches.
        if (!string.IsNullOrWhiteSpace(tag))
            query = query.Where(s => s.TagsJson != null && s.TagsJson.Contains(tag));

        var scenarios = await query
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Select(s => new PlaygroundScenarioSummaryResponse
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Tags = DeserializeTags(s.TagsJson),
                AgentName = s.AgentName,
                AiTaskId = s.AiTaskId,
                TurnCount = s.Turns.Count,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return scenarios;
    }

    public async Task<PlaygroundScenarioResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var scenario = await _dbContext.PlaygroundScenarios
            .AsNoTracking()
            .Include(s => s.Turns.OrderBy(t => t.SortOrder))
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        return scenario is null ? null : MapToResponse(scenario);
    }

    public async Task<PlaygroundScenarioResponse> CreateAsync(
        CreatePlaygroundScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var scenario = new PlaygroundScenario
        {
            TenantId = tenantId,
            Name = request.Name,
            Description = request.Description,
            TagsJson = SerializeTags(request.Tags),
            SystemPrompt = request.SystemPrompt,
            UserBriefJson = request.UserBriefJson,
            AgentName = request.AgentName,
            AiTaskId = request.AiTaskId,
            ModelId = request.ModelId,
            PromptVariablesJson = request.PromptVariables is { Count: > 0 }
                ? JsonSerializer.Serialize(request.PromptVariables, JsonOptions)
                : null,
        };

        for (var i = 0; i < request.Turns.Count; i++)
        {
            var turn = request.Turns[i];
            scenario.Turns.Add(new PlaygroundScenarioTurn
            {
                TenantId = tenantId,
                Role = turn.Role,
                Content = turn.Content,
                SortOrder = i,
            });
        }

        _dbContext.PlaygroundScenarios.Add(scenario);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created playground scenario '{Name}' ({Id}) with {TurnCount} turns",
            scenario.Name, scenario.Id, scenario.Turns.Count);

        return MapToResponse(scenario);
    }

    public async Task<PlaygroundScenarioResponse?> UpdateAsync(
        Guid id,
        UpdatePlaygroundScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var scenario = await _dbContext.PlaygroundScenarios
            .Include(s => s.Turns)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (scenario is null)
            return null;

        // Apply partial updates
        if (request.Name is not null)
            scenario.Name = request.Name;
        if (request.Description is not null)
            scenario.Description = request.Description;
        if (request.Tags is not null)
            scenario.TagsJson = SerializeTags(request.Tags);
        if (request.SystemPrompt is not null)
            scenario.SystemPrompt = request.SystemPrompt;
        if (request.UserBriefJson is not null)
            scenario.UserBriefJson = request.UserBriefJson;
        if (request.AgentName is not null)
            scenario.AgentName = request.AgentName;
        if (request.AiTaskId.HasValue)
            scenario.AiTaskId = request.AiTaskId;
        if (request.ModelId.HasValue)
            scenario.ModelId = request.ModelId;
        if (request.PromptVariables is not null)
            scenario.PromptVariablesJson = request.PromptVariables.Count > 0
                ? JsonSerializer.Serialize(request.PromptVariables, JsonOptions)
                : null;

        // Replace turns if provided (delete-and-recreate)
        if (request.Turns is not null)
        {
            _dbContext.PlaygroundScenarioTurns.RemoveRange(scenario.Turns);
            scenario.Turns.Clear();

            var tenantId = _tenantProvider.GetCurrentTenantId();
            for (var i = 0; i < request.Turns.Count; i++)
            {
                var turn = request.Turns[i];
                scenario.Turns.Add(new PlaygroundScenarioTurn
                {
                    TenantId = tenantId,
                    Role = turn.Role,
                    Content = turn.Content,
                    SortOrder = i,
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated playground scenario '{Name}' ({Id})",
            scenario.Name, scenario.Id);

        return MapToResponse(scenario);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var scenario = await _dbContext.PlaygroundScenarios
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (scenario is null)
            return false;

        _dbContext.PlaygroundScenarios.Remove(scenario);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted playground scenario '{Name}' ({Id})",
            scenario.Name, scenario.Id);

        return true;
    }

    // ── Mapping helpers ────────────────────────────────────────────────────

    private static PlaygroundScenarioResponse MapToResponse(PlaygroundScenario scenario)
    {
        return new PlaygroundScenarioResponse
        {
            Id = scenario.Id,
            Name = scenario.Name,
            Description = scenario.Description,
            Tags = DeserializeTags(scenario.TagsJson),
            SystemPrompt = scenario.SystemPrompt,
            UserBriefJson = scenario.UserBriefJson,
            AgentName = scenario.AgentName,
            AiTaskId = scenario.AiTaskId,
            ModelId = scenario.ModelId,
            PromptVariables = DeserializePromptVariables(scenario.PromptVariablesJson),
            Turns = scenario.Turns
                .OrderBy(t => t.SortOrder)
                .Select(t => new PlaygroundScenarioTurnResponse
                {
                    Id = t.Id,
                    Role = t.Role,
                    Content = t.Content,
                    SortOrder = t.SortOrder,
                })
                .ToList(),
            CreatedAt = scenario.CreatedAt,
            UpdatedAt = scenario.UpdatedAt,
        };
    }

    private static List<string> DeserializeTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string? SerializeTags(List<string>? tags)
    {
        if (tags is null or { Count: 0 })
            return null;
        return JsonSerializer.Serialize(tags, JsonOptions);
    }

    private static Dictionary<string, string>? DeserializePromptVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
