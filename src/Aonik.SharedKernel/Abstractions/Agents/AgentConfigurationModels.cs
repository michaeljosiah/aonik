namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Response DTO for an agent configuration. Represents a resolved or raw agent
/// config row from the database. <see cref="IsOverride"/> indicates whether this
/// is a tenant-specific override or a global/platform default.
/// </summary>
public sealed record AgentConfigurationResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public required string Description { get; init; }
    public required string InstructionsText { get; init; }
    public required string ToolsetIdsJson { get; init; }
    public required string PermissionsProfileJson { get; init; }
    public required string RiskTier { get; init; }
    public required bool IsActive { get; init; }
    public required Guid? TenantId { get; init; }

    /// <summary>
    /// Optional AI model ID assigned to this agent. When set, the orchestrator
    /// should use this model for the agent's LLM calls.
    /// </summary>
    public Guid? ModelId { get; init; }

    /// <summary>
    /// Resolved model name (e.g. "gpt-5-mini") for display purposes.
    /// Populated when <see cref="ModelId"/> is set and the model exists.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Optional URL for the agent's display icon/avatar image.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// True if this row is a tenant-specific override rather than the global default.
    /// </summary>
    public required bool IsOverride { get; init; }

    /// <summary>
    /// When <c>true</c>, the AG-UI endpoint injects a projected User Brief as a
    /// system message before the conversation history. Only user-facing product
    /// agents (e.g. personal-finance-agent) require this.
    /// </summary>
    public bool RequiresUserBrief { get; init; }

    /// <summary>
    /// Classifies this agent as an orchestrator or a sub-agent (domain specialist).
    /// </summary>
    public AgentType AgentType { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Request DTO for creating or updating a tenant-level agent configuration override.
/// All fields are optional — when <c>null</c>, the global default value is preserved.
/// </summary>
public sealed record UpsertAgentConfigurationRequest
{
    public string? Description { get; init; }
    public string? InstructionsText { get; init; }
    public string? ToolsetIdsJson { get; init; }
    public string? PermissionsProfileJson { get; init; }
    public string? RiskTier { get; init; }
    public bool? IsActive { get; init; }

    /// <summary>
    /// Optional AI model ID to assign to this agent. Set to <see cref="Guid.Empty"/>
    /// to clear the model assignment (revert to platform default).
    /// </summary>
    public Guid? ModelId { get; init; }

    /// <summary>
    /// Optional URL for the agent's display icon/avatar image.
    /// Set to empty string to clear the icon (revert to default).
    /// </summary>
    public string? IconUrl { get; init; }
}
