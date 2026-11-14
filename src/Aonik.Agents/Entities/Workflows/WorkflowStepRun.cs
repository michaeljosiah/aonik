using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities.Workflows;

public class WorkflowStepRun : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RunId { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public string InvocationPath { get; set; } = "root";
    public string Status { get; set; } = "Prepared";
    public string? ActionKey { get; set; }
    public int? ActionVersion { get; set; }
    public string? RequiredPermission { get; set; }
    public Guid OperationId { get; set; }
    public string? ArgumentsJson { get; set; }
    public string? ArgumentsHash { get; set; }
    public string? FactsHash { get; set; }
    public string? FactsJson { get; set; }
    public string? ExpectedSubjectVersion { get; set; }
    public string? WaitCheckpointId { get; set; }
    public string? ResultJson { get; set; }
    public string? DecisionTraceJson { get; set; }
    public string? SelectedRoute { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public Guid? ProposalId { get; set; }
    public Guid? AiRunId { get; set; }
    public int Attempts { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorCode { get; set; }
}
