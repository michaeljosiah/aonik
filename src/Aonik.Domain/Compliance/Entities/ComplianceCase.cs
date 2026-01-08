using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Compliance.Entities;

public class ComplianceCase : AuditableEntity, ITenantScoped
{
    public Guid ComplianceCaseId { get; set; }
    public Guid TenantId { get; set; }
    public string CaseType { get; set; } = string.Empty;
    public Guid? LinkedOrderId { get; set; }
    public Guid? LinkedPartyId { get; set; }
    public Guid? LinkedPaymentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string DetailsJson { get; set; } = string.Empty;
}
