using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Identity.Entities;

public class TenantCountry : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CountryId { get; set; }
}
