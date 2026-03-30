namespace Aonik.Ai.Contracts.Models;

// ── PromptSpec DTOs ─────────────────────────────────────────────────

public sealed record PromptSpecResponse
{
    public required Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string SystemTemplate { get; init; }
    public required string UserTemplate { get; init; }
    public required string DeveloperTemplate { get; init; }
    public required string VariablesSchemaJson { get; init; }
    public required string OutputSchemaJson { get; init; }
    public string? SafetyPolicyRef { get; init; }
    public required bool IsPublished { get; init; }
    public bool IsOverride => TenantId is not null;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record CreatePromptSpecRequest
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string SystemTemplate { get; init; }
    public string? UserTemplate { get; init; }
    public string? DeveloperTemplate { get; init; }
    public string? VariablesSchemaJson { get; init; }
    public string? OutputSchemaJson { get; init; }
    public string? SafetyPolicyRef { get; init; }
    public bool IsPublished { get; init; }
}

public sealed record UpdatePromptSpecRequest
{
    public string? SystemTemplate { get; init; }
    public string? UserTemplate { get; init; }
    public string? DeveloperTemplate { get; init; }
    public string? VariablesSchemaJson { get; init; }
    public string? OutputSchemaJson { get; init; }
    public string? SafetyPolicyRef { get; init; }
    public bool? IsPublished { get; init; }
}

// ── AiRoutePolicy DTOs ──────────────────────────────────────────────

public sealed record RoutePolicyResponse
{
    public required Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public required string UseCase { get; init; }
    public required string RiskTier { get; init; }
    public required string DataSensitivity { get; init; }
    public required decimal CostCeiling { get; init; }
    public required Guid PrimaryModelId { get; init; }
    public string? PrimaryModelName { get; init; }
    public required string FallbackModelIdsJson { get; init; }
    public required bool IsActive { get; init; }
    public bool IsOverride => TenantId is not null;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record CreateRoutePolicyRequest
{
    public required string UseCase { get; init; }
    public required string RiskTier { get; init; }
    public required string DataSensitivity { get; init; }
    public decimal CostCeiling { get; init; }
    public required Guid PrimaryModelId { get; init; }
    public string? FallbackModelIdsJson { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record UpdateRoutePolicyRequest
{
    public string? RiskTier { get; init; }
    public string? DataSensitivity { get; init; }
    public decimal? CostCeiling { get; init; }
    public Guid? PrimaryModelId { get; init; }
    public string? FallbackModelIdsJson { get; init; }
    public bool? IsActive { get; init; }
}
