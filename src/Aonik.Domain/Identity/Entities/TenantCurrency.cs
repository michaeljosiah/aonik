using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class TenantCurrency : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CurrencyId { get; set; }
}
