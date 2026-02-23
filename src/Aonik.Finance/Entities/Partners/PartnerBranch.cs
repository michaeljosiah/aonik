using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

public class PartnerBranch : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
}
