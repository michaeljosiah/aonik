using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Compliance.Entities;

public class ScreeningCheck : AuditableEntity
{
    public Guid ScreeningCheckId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PartyId { get; private set; }
    public string CheckType { get; private set; } = string.Empty;
    public string ResultStatus { get; private set; } = string.Empty;
    public string ResultJson { get; private set; } = string.Empty;
    public string? Decision { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTime? DecidedAt { get; private set; }

    private ScreeningCheck() { }

    public ScreeningCheck(Guid tenantId, Guid partyId, string checkType)
    {
        ScreeningCheckId = Id;
        TenantId = tenantId;
        PartyId = partyId;
        CheckType = checkType;
        ResultStatus = "Pending";
        ResultJson = "{}";
    }

    public void UpdateResult(string resultStatus, string resultJson)
    {
        ResultStatus = resultStatus;
        ResultJson = resultJson;
    }

    public void MakeDecision(string decision, Guid decidedBy)
    {
        Decision = decision;
        DecidedBy = decidedBy;
        DecidedAt = DateTime.UtcNow;
    }
}
