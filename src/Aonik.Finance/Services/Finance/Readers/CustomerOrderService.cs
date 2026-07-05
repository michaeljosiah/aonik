using Aonik.Finance.Contracts.Models.Orders;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ordering;
using FinanceOrderService = Aonik.Finance.Contracts.Services.Orders.IOrderService;

namespace Aonik.Finance.Services.Finance.Readers;

/// <summary>
/// Finance-side implementation of the customer-facing <see cref="ICustomerOrderService"/>. Delegates
/// to the module's rich <see cref="IOrderService"/> (which self-scopes the tenant via
/// <c>ITenantProvider</c>) and projects to the compact customer DTOs, enforcing owner-party scoping
/// on every read and on cancel. Lets PersonalFinance's "Simi" order tools surface a customer's own
/// orders without a project reference on <c>Aonik.Finance.Contracts</c>.
/// See <a href="../../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
internal sealed class CustomerOrderService : ICustomerOrderService
{
    private readonly FinanceOrderService _orderService;

    public CustomerOrderService(FinanceOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task<PagedResult<CustomerOrderSummary>> ListForPartyAsync(
        Guid partyId,
        string? status,
        string? orderType,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _orderService.ListOrdersAsync(
            new ListOrdersRequest(
                PageNumber: pageNumber,
                PageSize: pageSize,
                Status: status,
                OrderType: orderType,
                Search: null,
                PayerPartyId: partyId),
            cancellationToken);

        return new PagedResult<CustomerOrderSummary>(
            result.Items.Select(item => new CustomerOrderSummary(
                OrderId: item.OrderId,
                OrderType: item.OrderType,
                Status: item.Status,
                OriginCurrency: item.OriginCurrency,
                TotalAmountIn: item.TotalAmountIn,
                DestinationCurrency: item.DestinationCurrency,
                TotalAmountOut: item.TotalAmountOut,
                CreatedAt: item.CreatedAt,
                UpdatedAt: item.UpdatedAt)).ToList(),
            result.TotalCount,
            result.PageNumber,
            result.PageSize);
    }

    public async Task<CustomerOrderDetail?> GetForPartyAsync(
        Guid partyId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        BillPaymentOrderResponse order;
        try
        {
            order = await _orderService.GetOrderAsync(orderId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        if (order.PayerPartyId != partyId)
        {
            return null;
        }

        return MapDetail(order);
    }

    public async Task<CustomerOrderDetail> CancelForPartyAsync(
        Guid partyId,
        Guid orderId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var existing = await _orderService.GetOrderAsync(orderId, cancellationToken);
        if (existing.PayerPartyId != partyId)
        {
            throw new InvalidOperationException($"Order {orderId} not found.");
        }

        var cancelled = await _orderService.CancelOrderAsync(orderId, reason, cancellationToken);
        return MapDetail(cancelled);
    }

    private static CustomerOrderDetail MapDetail(BillPaymentOrderResponse order)
    {
        var firstItem = order.Items.OrderBy(item => item.ItemIndex).FirstOrDefault();

        return new CustomerOrderDetail(
            OrderId: order.OrderId,
            OrderType: order.OrderType,
            Status: order.Status,
            OriginCountry: order.OriginCountry,
            OriginCurrency: order.OriginCurrency,
            TotalAmountIn: order.TotalAmountIn,
            TotalFeesAmount: order.TotalFeesAmount,
            TotalAmountOut: order.TotalAmountOut,
            DestinationCurrency: order.DestinationCurrency,
            PurposeCode: order.PurposeCode,
            ItemCount: order.Items.Count,
            PrimaryReceiverName: firstItem?.ReceiverName,
            PrimaryBillerName: firstItem?.BillerName,
            CreatedAt: order.CreatedAt,
            SubmittedAt: order.SubmittedAt);
    }
}
