namespace Aonik.Ai.Contracts.Models;

// ── AiTask DTOs ────────────────────────────────────────────────────

public sealed record AiTaskResponse
{
    public required Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public required string UseCase { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string ExecutionMode { get; init; }
    public required string PromptName { get; init; }
    public required string PromptVersion { get; init; }
    public required string SystemTemplate { get; init; }
    public required string UserTemplate { get; init; }
    public required string DeveloperTemplate { get; init; }
    public required string VariablesSchemaJson { get; init; }
    public required string OutputSchemaJson { get; init; }
    public required bool IsPublished { get; init; }
    public required bool IsActive { get; init; }
    public string? PrimaryModelName { get; init; }
    public bool IsOverride => TenantId is not null;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record AiTaskStatsResponse
{
    public int TotalRuns { get; init; }
    public int Last24hRuns { get; init; }
    public double AvgLatencyMs { get; init; }
    public decimal AvgCost { get; init; }
    public double SuccessRate { get; init; }
    public DateTime? LastRunAt { get; init; }
}

public sealed record AiTaskDetailResponse
{
    public required Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public required string UseCase { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string ExecutionMode { get; init; }
    public required string PromptName { get; init; }
    public required string PromptVersion { get; init; }
    public required string SystemTemplate { get; init; }
    public required string UserTemplate { get; init; }
    public required string DeveloperTemplate { get; init; }
    public required string VariablesSchemaJson { get; init; }
    public required string OutputSchemaJson { get; init; }
    public required bool IsPublished { get; init; }
    public required bool IsActive { get; init; }
    public string? PrimaryModelName { get; init; }
    public bool IsOverride => TenantId is not null;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public required AiTaskStatsResponse Stats { get; init; }
    public Guid? RoutePolicyId { get; init; }
    public string? RoutePolicyRiskTier { get; init; }
    public string? RoutePolicyDataSensitivity { get; init; }
}

public sealed record CreateAiTaskRequest
{
    public required string UseCase { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required string ExecutionMode { get; init; }
    public required string PromptName { get; init; }
    public required string PromptVersion { get; init; }
    public required string SystemTemplate { get; init; }
    public string? UserTemplate { get; init; }
    public string? DeveloperTemplate { get; init; }
    public string? VariablesSchemaJson { get; init; }
    public string? OutputSchemaJson { get; init; }
    public bool IsPublished { get; init; } = true;
    public bool IsActive { get; init; } = true;
}

public sealed record UpdateAiTaskRequest
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? ExecutionMode { get; init; }
    public string? PromptName { get; init; }
    public string? PromptVersion { get; init; }
    public string? SystemTemplate { get; init; }
    public string? UserTemplate { get; init; }
    public string? DeveloperTemplate { get; init; }
    public string? VariablesSchemaJson { get; init; }
    public string? OutputSchemaJson { get; init; }
    public bool? IsPublished { get; init; }
    public bool? IsActive { get; init; }
}
