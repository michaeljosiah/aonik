using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

public class PayoutSchema : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SchemaJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
