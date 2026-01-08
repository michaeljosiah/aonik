using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class EvalRun : AuditableEntity
{
    public Guid EvalRunId { get; private set; }
    public Guid EvalSuiteId { get; private set; }
    public Guid AiModelId { get; private set; }
    public Guid? PromptSpecId { get; private set; }
    public string ResultsJson { get; private set; } = string.Empty;
    public bool PassFail { get; private set; }
    public DateTime RanAt { get; private set; }

    private EvalRun() { }

    public EvalRun(Guid evalSuiteId, Guid aiModelId, Guid? promptSpecId = null)
    {
        EvalRunId = Id;
        EvalSuiteId = evalSuiteId;
        AiModelId = aiModelId;
        PromptSpecId = promptSpecId;
        ResultsJson = "{}";
        RanAt = DateTime.UtcNow;
    }

    public void RecordResults(string resultsJson, bool passFail)
    {
        ResultsJson = resultsJson;
        PassFail = passFail;
    }
}
