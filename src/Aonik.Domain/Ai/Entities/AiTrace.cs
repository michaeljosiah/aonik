using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiTrace : AuditableEntity
{
    public Guid AiTraceId { get; private set; }
    public Guid AiRunId { get; private set; }
    public string StepsJson { get; private set; } = string.Empty;
    public string ToolCallsJson { get; private set; } = string.Empty;
    public string? IntermediateReasoningRef { get; private set; }

    private AiTrace() { }

    public AiTrace(Guid aiRunId, string stepsJson, string toolCallsJson)
    {
        AiTraceId = Id;
        AiRunId = aiRunId;
        StepsJson = stepsJson;
        ToolCallsJson = toolCallsJson;
    }

    public void UpdateSteps(string stepsJson)
    {
        StepsJson = stepsJson;
    }

    public void UpdateToolCalls(string toolCallsJson)
    {
        ToolCallsJson = toolCallsJson;
    }

    public void UpdateIntermediateReasoningRef(string intermediateReasoningRef)
    {
        IntermediateReasoningRef = intermediateReasoningRef;
    }
}
