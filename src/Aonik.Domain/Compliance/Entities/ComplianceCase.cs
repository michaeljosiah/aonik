using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Compliance.Entities;

public class ComplianceCase : AuditableEntity, ITenantScoped
{
    public Guid ComplianceCaseId { get; private set; }
    public Guid TenantId { get; private set; }
    public string CaseType { get; private set; } = string.Empty;
    public Guid? LinkedOrderId { get; private set; }
    public Guid? LinkedPartyId { get; private set; }
    public Guid? LinkedPaymentId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? Summary { get; private set; }
    public string DetailsJson { get; private set; } = string.Empty;

    private ComplianceCase() { }

    public ComplianceCase(Guid tenantId, string caseType, Guid? linkedOrderId = null, Guid? linkedPartyId = null, Guid? linkedPaymentId = null)
    {
        ComplianceCaseId = Id;
        TenantId = tenantId;
        CaseType = caseType;
        LinkedOrderId = linkedOrderId;
        LinkedPartyId = linkedPartyId;
        LinkedPaymentId = linkedPaymentId;
        Status = "Open";
        DetailsJson = "{}";
    }

    public void UpdateSummary(string summary)
    {
        Summary = summary;
    }

    public void UpdateDetails(string detailsJson)
    {
        DetailsJson = detailsJson;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void Close()
    {
        Status = "Closed";
    }

    public void Escalate()
    {
        Status = "Escalated";
    }
}
