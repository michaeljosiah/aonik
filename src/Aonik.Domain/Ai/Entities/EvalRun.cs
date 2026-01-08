using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class EvalRun : AuditableEntity
{
    public Guid EvalRunId { get; set; }
    public Guid EvalSuiteId { get; set; }
    public Guid AiModelId { get; set; }
    public Guid? PromptSpecId { get; set; }
    public string ResultsJson { get; set; } = string.Empty;
    public bool PassFail { get; set; }
    public DateTime RanAt { get; set; }
}
