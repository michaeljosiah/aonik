namespace Aonik.Ai.Contracts.Models;

// ── AiRun DTOs ─────────────────────────────────────────────────────

public sealed record AiRunSummaryResponse
{
    public required Guid Id { get; init; }
    public required string UseCase { get; init; }
    public string? ModelName { get; init; }
    public required int TokensUsed { get; init; }
    public required decimal CostEstimate { get; init; }
    public required int LatencyMs { get; init; }
    public required string Outcome { get; init; }
    public DateTime CreatedAt { get; init; }
}
