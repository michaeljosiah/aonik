using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Compliance.Entities;

public class ScreeningCheck : AuditableEntity, ITenantScoped
{
    public Guid ScreeningCheckId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PartyId { get; set; }
    public string CheckType { get; set; } = string.Empty;
    public string ResultStatus { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public string? Decision { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
}
