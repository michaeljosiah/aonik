using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Partners.Entities;

public class Partner : AuditableEntity, ITenantScoped
{
    public Guid PartnerId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CapabilitiesJson { get; set; } = string.Empty;
    public string OperatingHoursJson { get; set; } = string.Empty;
    public List<PartnerBranch> Branches { get; set; } = new();
}
