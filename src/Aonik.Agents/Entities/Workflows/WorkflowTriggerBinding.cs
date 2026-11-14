using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities.Workflows;

public sealed class WorkflowTriggerBinding : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid WorkflowId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public int DraftRevision { get; set; }
    public string DraftJson { get; set; } = "{}";
    public string DraftHash { get; set; } = string.Empty;
    public int? ActiveRevision { get; set; }
    public int DisabledThroughRevision { get; set; }
    public string? ActiveJson { get; set; }
    public string? ActiveHash { get; set; }
    public string? ActiveSource { get; set; }
    public Guid? ActiveAutomationIdentityId { get; set; }
    public Guid ConfiguredByUserId { get; set; }
    public Guid? ActivatedByUserId { get; set; }
    public Guid? ActivationProposalId { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public Guid? DisabledByUserId { get; set; }
    public DateTime? DisabledAt { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public long AdmittedRuns { get; set; }
    public string? LastError { get; set; }
}
