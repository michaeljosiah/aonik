using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Partners.Entities;

public class PayoutSchema : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SchemaJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
