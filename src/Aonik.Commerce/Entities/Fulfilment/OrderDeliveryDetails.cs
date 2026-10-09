using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Fulfilment;

/// <summary>Commerce-owned delivery facts frozen at checkout, soft-linked to the Order spine.</summary>
public class OrderDeliveryDetails : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string PurchaserEmail { get; set; } = string.Empty;
    public string PurchaserFirstName { get; set; } = string.Empty;
    public string PurchaserLastName { get; set; } = string.Empty;
    public string PurchaserPhone { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string Postcode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public DateOnly DeliveryDate { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsGift { get; set; }
    public bool HidePrices { get; set; }
    public bool IncludeGreetingCard { get; set; }
    public string? GreetingCardMessage { get; set; }
}
