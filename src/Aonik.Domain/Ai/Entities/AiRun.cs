using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiRun : AuditableEntity, ITenantScoped
{
    public Guid AiRunId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string UseCase { get; private set; } = string.Empty;
    public Guid AiModelId { get; private set; }
    public Guid? PromptSpecId { get; private set; }
    public Guid? AiPolicyId { get; private set; }
    public string InputRefsJson { get; private set; } = string.Empty;
    public string? OutputRef { get; private set; }
    public int TokensUsed { get; private set; }
    public decimal CostEstimate { get; private set; }
    public int LatencyMs { get; private set; }
    public string Outcome { get; private set; } = string.Empty;

    private AiRun() { }

    public AiRun(Guid tenantId, string useCase, Guid aiModelId, Guid? userId = null, Guid? promptSpecId = null, Guid? aiPolicyId = null)
    {
        AiRunId = Id;
        TenantId = tenantId;
        UserId = userId;
        UseCase = useCase;
        AiModelId = aiModelId;
        PromptSpecId = promptSpecId;
        AiPolicyId = aiPolicyId;
        InputRefsJson = "{}";
        Outcome = "Pending";
    }

    public void RecordCompletion(string outputRef, int tokensUsed, decimal costEstimate, int latencyMs, string outcome)
    {
        OutputRef = outputRef;
        TokensUsed = tokensUsed;
        CostEstimate = costEstimate;
        LatencyMs = latencyMs;
        Outcome = outcome;
    }

    public void UpdateInputRefs(string inputRefsJson)
    {
        InputRefsJson = inputRefsJson;
    }
}
