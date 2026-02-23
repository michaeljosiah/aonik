using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Compliance;

public class DocumentVerification : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid DocumentUsageId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? DecisionReasonCode { get; set; }
    public string? DecisionNotes { get; set; }
    public string VerifierType { get; set; } = string.Empty;
    public string? VerifierId { get; set; }
    public Guid? AiRunId { get; set; }
    public DocumentUsage? DocumentUsage { get; set; }
}
