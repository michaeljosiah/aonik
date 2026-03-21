namespace Aonik.Agents.Contracts.Models;

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
    /// True if this row is a tenant-specific override rather than the global default.
    /// </summary>
    public required bool IsOverride { get; init; }

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
}
