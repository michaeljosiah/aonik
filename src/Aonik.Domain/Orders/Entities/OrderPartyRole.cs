using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Orders.Entities;

public class OrderPartyRole : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid PartyId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = string.Empty;
}
