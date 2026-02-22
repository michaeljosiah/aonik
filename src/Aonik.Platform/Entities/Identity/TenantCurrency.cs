using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Identity;

public class TenantCurrency : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CurrencyId { get; set; }
}
