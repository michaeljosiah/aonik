namespace Aonik.Ai.Contracts.Models;

// ── Provider DTOs ────────────────────────────────────────────────────

public sealed record AiProviderResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? AuthConfigRef { get; init; }
    public required string CapabilitiesJson { get; init; }
    public required bool IsActive { get; init; }
    public required List<AiModelResponse> Models { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record CreateAiProviderRequest
{
    public required string Name { get; init; }
    public string? AuthConfigRef { get; init; }
    public string CapabilitiesJson { get; init; } = "[]";
    public bool IsActive { get; init; } = true;
}

public sealed record UpdateAiProviderRequest
{
    public string? Name { get; init; }
    public string? AuthConfigRef { get; init; }
    public string? CapabilitiesJson { get; init; }
    public bool? IsActive { get; init; }
}

// ── Model DTOs ───────────────────────────────────────────────────────

public sealed record AiModelResponse
{
    public required Guid Id { get; init; }
    public required Guid AiProviderId { get; init; }
    public string? ProviderName { get; init; }
    public required string ModelName { get; init; }
    public required int ContextWindow { get; init; }
    public required string CostProfileJson { get; init; }
    public required string LatencyProfileJson { get; init; }
    public required string PolicyTagsJson { get; init; }
    public required bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record CreateAiModelRequest
{
    public required Guid AiProviderId { get; init; }
    public required string ModelName { get; init; }
    public int ContextWindow { get; init; }
    public string CostProfileJson { get; init; } = "{}";
    public string LatencyProfileJson { get; init; } = "{}";
    public string PolicyTagsJson { get; init; } = "[]";
    public bool IsActive { get; init; } = true;
}

public sealed record UpdateAiModelRequest
{
    public string? ModelName { get; init; }
    public int? ContextWindow { get; init; }
    public string? CostProfileJson { get; init; }
    public string? LatencyProfileJson { get; init; }
    public string? PolicyTagsJson { get; init; }
    public bool? IsActive { get; init; }
}
