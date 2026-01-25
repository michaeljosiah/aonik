using ApiContracts = Aonik.Api.Contracts.Orders;
using AppModels = Aonik.Application.Models.Orders;

namespace Aonik.Api.Endpoints.Orders;

internal static class OrderMapping
{
    public static AppModels.CreateBillPaymentOrderRequest ToAppRequest(
        ApiContracts.CreateBillPaymentOrderRequest request,
        string? idempotencyKey)
    {
        return new AppModels.CreateBillPaymentOrderRequest(
            request.PayerPartyId,
            request.OriginCountry,
            request.OriginCurrency,
            request.PurposeCode,
            request.Notes,
            idempotencyKey,
            request.Items?.Select(ToAppRequest).ToList());
    }

    public static AppModels.CreateBillPaymentItemRequest ToAppRequest(ApiContracts.CreateBillPaymentItemRequest request)
    {
        return new AppModels.CreateBillPaymentItemRequest(
            request.BillerId,
            request.ServiceId,
            request.ServiceCode,
            request.ServiceFieldValues,
            request.ReceiverPartyId,
            request.NewReceiver == null
                ? null
                : new AppModels.CreateReceiverRequest(
                    request.NewReceiver.DisplayName,
                    request.NewReceiver.PartyType,
                    request.NewReceiver.FirstName,
                    request.NewReceiver.LastName,
                    request.NewReceiver.Phone,
                    request.NewReceiver.Email,
                    request.NewReceiver.CountryCode),
            request.RelationshipTypeCode,
            request.OriginAmount,
            request.DestinationAmount,
            request.DestinationCurrency,
            request.DestinationCountry,
            request.PricingQuoteId,
            request.PurposeCode,
            request.Notes);
    }

    public static AppModels.UpdateBillPaymentItemRequest ToAppRequest(ApiContracts.UpdateBillPaymentItemRequest request)
    {
        return new AppModels.UpdateBillPaymentItemRequest(
            request.ServiceFieldValues,
            request.ReceiverPartyId,
            request.RelationshipTypeCode,
            request.OriginAmount,
            request.DestinationAmount,
            request.PricingQuoteId,
            request.PurposeCode,
            request.Notes);
    }

    public static ApiContracts.BillPaymentOrderResponse ToApiResponse(AppModels.BillPaymentOrderResponse response)
    {
        return new ApiContracts.BillPaymentOrderResponse(
            response.OrderId,
            response.OrderType,
            response.Status,
            response.PayerPartyId,
            response.PayerName,
            response.OriginCountry,
            response.OriginCurrency,
            response.TotalAmountIn,
            response.TotalFeesAmount,
            response.TotalAmountOut,
            response.DestinationCurrency,
            response.PurposeCode,
            response.CreatedAt,
            response.SubmittedAt,
            response.Items.Select(ToApiResponse).ToList());
    }

    public static ApiContracts.OrderItemResponse ToApiResponse(AppModels.OrderItemResponse response)
    {
        return new ApiContracts.OrderItemResponse(
            response.OrderItemId,
            response.ItemIndex,
            response.ItemType,
            response.Status,
            response.BillerId,
            response.BillerName,
            response.ServiceId,
            response.ServiceName,
            response.ServiceFieldValues,
            response.ReceiverPartyId,
            response.ReceiverName,
            response.RelationshipTypeCode,
            response.AmountIn,
            response.CurrencyIn,
            response.AmountOut,
            response.CurrencyOut,
            response.FeesTotal,
            response.ExchangeRate,
            response.PricingQuoteId,
            response.QuoteExpiresAt,
            response.IsQuoteExpired);
    }
}
