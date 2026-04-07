using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

/// <summary>
/// A saved, reusable conversation setup for the AI Playground.
/// Captures the full playground state: conversation turns, system prompt,
/// optional user brief, agent/task binding, and organisational tags.
/// Tenant-scoped — each tenant manages its own scenario library.
/// </summary>
public class PlaygroundScenario : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Human-readable scenario name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional longer description of what this scenario tests.</summary>
    public string? Description { get; set; }

    /// <summary>JSON array of tag strings for filtering and organisation, e.g. ["billing","multi-turn"].</summary>
    public string? TagsJson { get; set; }

    /// <summary>The system prompt captured at save time.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>Optional user brief JSON payload.</summary>
    public string? UserBriefJson { get; set; }

    /// <summary>Optional agent reference (for agent-mode scenarios).</summary>
    public string? AgentName { get; set; }

    /// <summary>Optional AI task reference (for task-mode scenarios).</summary>
    public Guid? AiTaskId { get; set; }

    /// <summary>Optional model override.</summary>
    public Guid? ModelId { get; set; }

    /// <summary>Serialised prompt variables for AI Task mode.</summary>
    public string? PromptVariablesJson { get; set; }

    /// <summary>The conversation turns within this scenario.</summary>
    public List<PlaygroundScenarioTurn> Turns { get; set; } = new();
}
