using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Orders.Entities;

public class OrderPartyRole : AuditableEntity, ITenantScoped
{
    public Guid OrderPartyRoleId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid PartyId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string DetailsJson { get; private set; } = string.Empty;

    private OrderPartyRole() { }

    public OrderPartyRole(Guid tenantId, Guid orderId, Guid partyId, string role)
    {
        OrderPartyRoleId = Id;
        TenantId = tenantId;
        OrderId = orderId;
        PartyId = partyId;
        Role = role;
        DetailsJson = "{}";
    }

    public void UpdateRole(string role)
    {
        Role = role;
    }

    public void UpdateDetails(string detailsJson)
    {
        DetailsJson = detailsJson;
    }
}
