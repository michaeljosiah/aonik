namespace Aonik.Agents.Contracts.Models;

/// <summary>
/// Summary DTO for an agent run, used in list views.
/// Contains key metadata without the full plan/steps payloads.
/// </summary>
public sealed record AgentRunSummary
{
    public required Guid Id { get; init; }
    public required Guid AgentId { get; init; }
    public required string Goal { get; init; }
    public required string Status { get; init; }
    public required int StepCount { get; init; }
    public required int LinkedAiRunCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
