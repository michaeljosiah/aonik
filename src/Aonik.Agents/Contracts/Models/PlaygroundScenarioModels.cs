namespace Aonik.Agents.Contracts.Models;

// ── Response DTOs ──────────────────────────────────────────────────────────────

/// <summary>
/// Full scenario response including turns. Returned by Get and Create/Update.
/// </summary>
public sealed record PlaygroundScenarioResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public List<string> Tags { get; init; } = [];
    public string? SystemPrompt { get; init; }
    public string? UserBriefJson { get; init; }
    public string? AgentName { get; init; }
    public Guid? AiTaskId { get; init; }
    public Guid? ModelId { get; init; }
    public Dictionary<string, string>? PromptVariables { get; init; }
    public List<PlaygroundScenarioTurnResponse> Turns { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// A single turn within a scenario response.
/// </summary>
public sealed record PlaygroundScenarioTurnResponse
{
    public Guid Id { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

/// <summary>
/// Summary DTO for list endpoint — excludes turns for efficiency.
/// </summary>
public sealed record PlaygroundScenarioSummaryResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public List<string> Tags { get; init; } = [];
    public string? AgentName { get; init; }
    public Guid? AiTaskId { get; init; }
    public int TurnCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

// ── Request DTOs ───────────────────────────────────────────────────────────────

/// <summary>
/// Request to create a new playground scenario (e.g. from "Save as Scenario").
/// </summary>
public sealed record CreatePlaygroundScenarioRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public List<string>? Tags { get; init; }
    public string? SystemPrompt { get; init; }
    public string? UserBriefJson { get; init; }
    public string? AgentName { get; init; }
    public Guid? AiTaskId { get; init; }
    public Guid? ModelId { get; init; }
    public Dictionary<string, string>? PromptVariables { get; init; }
    public List<CreatePlaygroundScenarioTurnRequest> Turns { get; init; } = [];
}

/// <summary>
/// A single turn within a create scenario request.
/// </summary>
public sealed record CreatePlaygroundScenarioTurnRequest
{
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// Request to update an existing scenario. All fields are optional (partial update).
/// </summary>
public sealed record UpdatePlaygroundScenarioRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public List<string>? Tags { get; init; }
    public string? SystemPrompt { get; init; }
    public string? UserBriefJson { get; init; }
    public string? AgentName { get; init; }
    public Guid? AiTaskId { get; init; }
    public Guid? ModelId { get; init; }
    public Dictionary<string, string>? PromptVariables { get; init; }
    public List<CreatePlaygroundScenarioTurnRequest>? Turns { get; init; }
}

/// <summary>
/// Request to generate a scenario via the AI wizard.
/// </summary>
public sealed record GeneratePlaygroundScenarioRequest
{
    /// <summary>Natural language instructions describing the desired scenario.</summary>
    public string Instructions { get; init; } = string.Empty;

    /// <summary>Optional agent context to scope the generation.</summary>
    public string? AgentName { get; init; }

    /// <summary>Optional AI task context to scope the generation.</summary>
    public Guid? AiTaskId { get; init; }

    /// <summary>Model to use for generation (defaults to system default).</summary>
    public Guid? ModelId { get; init; }
}
