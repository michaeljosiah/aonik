using ApiContracts = Aonik.Api.Contracts.Orders;
using ApiPricing = Aonik.Api.Contracts.Pricing;
using AppModels = Aonik.Application.Models.Orders;
using AppPricing = Aonik.Application.Models.Pricing;

namespace Aonik.Api.Endpoints.Orders;

internal static class OrderMapping
{
    public static AppModels.OrderDetails ToApp(ApiContracts.OrderDetails details)
    {
        return new AppModels.OrderDetails(
            details.BillPayment == null ? null : new AppModels.BillPaymentDetails(
                details.BillPayment.BillerId,
                details.BillPayment.BillReference,
                details.BillPayment.BillerAccountId,
                details.BillPayment.BillerCategory,
                details.BillPayment.BillerCountry),
            details.BankTransfer == null ? null : new AppModels.BankTransferDetails(
                details.BankTransfer.DestinationAccountId,
                details.BankTransfer.DestinationAccountNumber,
                details.BankTransfer.DestinationBankCode,
                details.BankTransfer.DestinationCountry,
                details.BankTransfer.Purpose),
            details.CashCollection == null ? null : new AppModels.CashCollectionDetails(
                details.CashCollection.RecipientId,
                details.CashCollection.PickupLocation,
                details.CashCollection.PickupToken,
                details.CashCollection.SenderId));
    }

    public static AppModels.PartyRef? ToApp(ApiContracts.PartyRef? party)
    {
        return party == null ? null : new AppModels.PartyRef(party.PartyId, party.DisplayName, party.Reference);
    }

    public static AppModels.OrderItemRequest ToApp(ApiContracts.OrderItemRequest item)
    {
        return new AppModels.OrderItemRequest(
            item.ItemType,
            item.Reference,
            item.Amount,
            item.Currency,
            item.Metadata);
    }

    public static ApiContracts.OrderDetailResponse ToApi(AppModels.OrderDetailResponse response)
    {
        return new ApiContracts.OrderDetailResponse(
            response.OrderId,
            response.TenantId,
            response.OrderNumber,
            response.InvoiceId,
            response.Status,
            response.PaymentStatus,
            response.InvoiceStatus,
            response.OrderType,
            response.ServiceCode,
            ToApi(response.Details),
            response.Items.Select(ToApi).ToList(),
            ToApi(response.Amounts),
            ToApi(response.Fees),
            ToApi(response.Fx),
            response.PaymentIntentId,
            response.PayoutId,
            response.PayoutStatus,
            response.LedgerReference == null ? null : new ApiContracts.LedgerReference(
                response.LedgerReference.JournalId,
                response.LedgerReference.EntryIds),
            response.CreatedAt);
    }

    public static ApiContracts.OrderListResponse ToApi(AppModels.OrderListResponse response)
    {
        return new ApiContracts.OrderListResponse(
            response.Items.Select(ToApi).ToList(),
            response.TotalCount,
            response.PageNumber,
            response.PageSize);
    }

    public static ApiContracts.OrderSummaryResponse ToApi(AppModels.OrderSummaryResponse response)
    {
        return new ApiContracts.OrderSummaryResponse(
            response.OrderId,
            response.TenantId,
            response.OrderNumber,
            response.InvoiceId,
            response.Status,
            response.PaymentStatus,
            response.InvoiceStatus,
            response.OrderType,
            response.ServiceCode,
            ToApi(response.Details),
            ToApi(response.Amounts),
            response.CreatedAt);
    }

    public static ApiContracts.OrderDetails ToApi(AppModels.OrderDetails details)
    {
        return new ApiContracts.OrderDetails(
            details.BillPayment == null ? null : new ApiContracts.BillPaymentDetails(
                details.BillPayment.BillerId,
                details.BillPayment.BillReference,
                details.BillPayment.BillerAccountId,
                details.BillPayment.BillerCategory,
                details.BillPayment.BillerCountry),
            details.BankTransfer == null ? null : new ApiContracts.BankTransferDetails(
                details.BankTransfer.DestinationAccountId,
                details.BankTransfer.DestinationAccountNumber,
                details.BankTransfer.DestinationBankCode,
                details.BankTransfer.DestinationCountry,
                details.BankTransfer.Purpose),
            details.CashCollection == null ? null : new ApiContracts.CashCollectionDetails(
                details.CashCollection.RecipientId,
                details.CashCollection.PickupLocation,
                details.CashCollection.PickupToken,
                details.CashCollection.SenderId));
    }

    public static ApiContracts.OrderItemResponse ToApi(AppModels.OrderItemResponse item)
    {
        return new ApiContracts.OrderItemResponse(
            item.OrderItemId,
            item.ItemType,
            item.Reference,
            item.Amount,
            item.Currency,
            item.Metadata);
    }

    public static ApiContracts.OrderAmountSnapshot ToApi(AppModels.OrderAmountSnapshot snapshot)
    {
        return new ApiContracts.OrderAmountSnapshot(
            snapshot.Amount,
            snapshot.Currency,
            snapshot.TotalAmount);
    }

    public static ApiContracts.OrderFeeSnapshot ToApi(AppModels.OrderFeeSnapshot snapshot)
    {
        return new ApiContracts.OrderFeeSnapshot(
            snapshot.FeesTotal,
            ToApi(snapshot.FeeBreakdown));
    }

    public static ApiContracts.OrderFxSnapshot ToApi(AppModels.OrderFxSnapshot snapshot)
    {
        return new ApiContracts.OrderFxSnapshot(
            snapshot.ExchangeRate,
            snapshot.RateMarkup);
    }

    public static IReadOnlyCollection<AppPricing.FeeBreakdownItem>? ToApp(
        IReadOnlyCollection<ApiPricing.FeeBreakdownItem>? items)
    {
        return items?.Select(item => new AppPricing.FeeBreakdownItem(
            item.Code,
            item.Description,
            item.Amount,
            item.Currency,
            item.CalculationType)).ToList();
    }

    public static IReadOnlyCollection<ApiPricing.FeeBreakdownItem>? ToApi(
        IReadOnlyCollection<AppPricing.FeeBreakdownItem>? items)
    {
        return items?.Select(item => new ApiPricing.FeeBreakdownItem(
            item.Code,
            item.Description,
            item.Amount,
            item.Currency,
            item.CalculationType)).ToList();
    }
}
