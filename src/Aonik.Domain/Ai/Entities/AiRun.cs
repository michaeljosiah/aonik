using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ai.Entities;

public class AiRun : AuditableEntity, ITenantScoped
{
    public Guid AiRunId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string UseCase { get; set; } = string.Empty;
    public Guid AiModelId { get; set; }
    public Guid? PromptSpecId { get; set; }
    public Guid? AiPolicyId { get; set; }
    public string InputRefsJson { get; set; } = string.Empty;
    public string? OutputRef { get; set; }
    public int TokensUsed { get; set; }
    public decimal CostEstimate { get; set; }
    public int LatencyMs { get; set; }
    public string Outcome { get; set; } = string.Empty;
}
