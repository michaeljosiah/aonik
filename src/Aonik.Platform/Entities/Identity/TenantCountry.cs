using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Identity;

public class TenantCountry : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CountryId { get; set; }
}
