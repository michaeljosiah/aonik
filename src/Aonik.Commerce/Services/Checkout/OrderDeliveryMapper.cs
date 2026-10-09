using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Fulfilment;

namespace Aonik.Commerce.Services.Checkout;

internal static class OrderDeliveryMapper
{
    public static OrderDeliveryDto Map(OrderDeliveryDetails delivery) => new(
        new CheckoutContactDto(delivery.PurchaserEmail, delivery.PurchaserFirstName,
            delivery.PurchaserLastName, delivery.PurchaserPhone),
        new DeliveryAddressDto(delivery.AddressLine1, delivery.AddressLine2, delivery.City,
            delivery.Region, delivery.Postcode, delivery.CountryCode),
        delivery.DeliveryDate, delivery.Timezone,
        new DeliveryRecipientDto(delivery.RecipientName, delivery.RecipientPhone), delivery.Notes,
        delivery.IsGift ? new OrderGiftDto(delivery.HidePrices, delivery.IncludeGreetingCard,
            delivery.IncludeGreetingCard ? delivery.GreetingCardMessage : null) : null,
        delivery.AcceptedTermsVersion is { } version && delivery.AcceptedTermsUrl is { } url
            && delivery.TermsAcceptedAtUtc is { } acceptedAt ? new AcceptedSaleTermsDto(version, url, acceptedAt) : null);

    public static OrderDeliveryDetails Create(Guid tenantId, Guid orderId, OrderDeliveryDto delivery) => new()
    {
        TenantId = tenantId,
        OrderId = orderId,
        PurchaserEmail = delivery.Purchaser.Email,
        PurchaserFirstName = delivery.Purchaser.FirstName,
        PurchaserLastName = delivery.Purchaser.LastName,
        PurchaserPhone = delivery.Purchaser.Phone,
        AddressLine1 = delivery.Address.Line1,
        AddressLine2 = delivery.Address.Line2,
        City = delivery.Address.City,
        Region = delivery.Address.Region,
        Postcode = delivery.Address.Postcode,
        CountryCode = delivery.Address.CountryCode,
        DeliveryDate = delivery.DeliveryDate,
        Timezone = delivery.Timezone,
        RecipientName = delivery.Recipient.Name,
        RecipientPhone = delivery.Recipient.Phone,
        Notes = delivery.Notes,
        IsGift = delivery.Gift is not null,
        HidePrices = delivery.Gift?.HidePrices ?? false,
        IncludeGreetingCard = delivery.Gift?.IncludeGreetingCard ?? false,
        GreetingCardMessage = delivery.Gift is { IncludeGreetingCard: true } gift ? gift.GreetingCardMessage : null,
        AcceptedTermsVersion = delivery.SaleTerms?.Version,
        AcceptedTermsUrl = delivery.SaleTerms?.Url,
        TermsAcceptedAtUtc = delivery.SaleTerms?.AcceptedAtUtc
    };
}
