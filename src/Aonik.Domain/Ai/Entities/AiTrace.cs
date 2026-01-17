using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiTrace : AuditableEntity
{
    public Guid AiRunId { get; set; }
    public string StepsJson { get; set; } = string.Empty;
    public string ToolCallsJson { get; set; } = string.Empty;
    public string? IntermediateReasoningRef { get; set; }
}
