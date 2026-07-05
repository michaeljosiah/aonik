using System.ComponentModel;
using Aonik.Finance.Contracts.Models.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// Personal-finance order tools — the current user's payment orders (bill
/// payments, transfers, remittances) with ownership-scoped reads and unsettled
/// cancellation (read + mutating). Registered by
/// <see cref="PersonalFinanceTools.CreateAll"/>.
/// </summary>
internal sealed class PersonalFinanceOrderTools
{
    private readonly IOrderService _orderService;
    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PersonalFinanceOrderTools(
        IOrderService orderService,
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _orderService = orderService;
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    // ── Order Read Tools ──────────────────────────────────────────

    [Description("Lists the current user's payment orders (bill payments, transfers, remittances), most recent first. Returns compact summaries — do NOT dump full payloads on the user; use this to answer questions like 'what's the status of my recent payments', 'did my transfer go through', or 'show me pending orders'. Filter by status ('Draft', 'Submitted', 'Processing', 'Completed', 'Settled', 'Cancelled', 'Failed') or orderType ('BillPayment', 'Transfer'). Results are automatically scoped to the current user's party — orders belonging to other users in the tenant are never returned.")]
    public async Task<IReadOnlyList<OrderSummary>> ListOrders(
        [Description("Optional order status filter. Examples: 'Submitted', 'Processing', 'Completed', 'Cancelled', 'Failed'.")] string? status = null,
        [Description("Optional order type filter. Examples: 'BillPayment', 'Transfer'.")] string? orderType = null,
        [Description("Page size (1-50). Defaults to 20.")] int pageSize = 20,
        [Description("Page number (1-based). Defaults to 1.")] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var partyId = await ResolveCurrentPartyIdAsync(cancellationToken);
        if (partyId is null)
        {
            return Array.Empty<OrderSummary>();
        }

        var limit = Math.Clamp(pageSize, 1, 50);
        var page = pageNumber < 1 ? 1 : pageNumber;

        var result = await _orderService.ListOrdersAsync(
            new ListOrdersRequest(
                PageNumber: page,
                PageSize: limit,
                Status: status,
                OrderType: orderType,
                Search: null,
                PayerPartyId: partyId),
            cancellationToken);

        return result.Items
            .Select(item => new OrderSummary(
                OrderId: item.OrderId,
                OrderType: item.OrderType,
                Status: item.Status,
                OriginCurrency: item.OriginCurrency,
                TotalAmountIn: item.TotalAmountIn,
                DestinationCurrency: item.DestinationCurrency,
                TotalAmountOut: item.TotalAmountOut,
                CreatedAt: item.CreatedAt,
                UpdatedAt: item.UpdatedAt))
            .ToArray();
    }

    [Description("Retrieves the summary of a single order by its unique identifier — compact shape only (status, amounts, item count, top receiver). Use this when the user asks about a specific order; do not dump the full payload. Ownership is verified: only returns data when the order belongs to the current user's party.")]
    public async Task<OrderDetailSummary?> GetOrder(
        [Description("The unique identifier (GUID) of the order")] Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var partyId = await ResolveCurrentPartyIdAsync(cancellationToken);
        if (partyId is null)
        {
            return null;
        }

        BillPaymentOrderResponse order;
        try
        {
            order = await _orderService.GetOrderAsync(orderId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        if (order.PayerPartyId != partyId.Value)
        {
            return null;
        }

        var firstItem = order.Items.OrderBy(item => item.ItemIndex).FirstOrDefault();

        return new OrderDetailSummary(
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

    // ── Order Mutating Tools ──────────────────────────────────────

    [Description("Cancels a payment order that has not yet settled. No-op for orders already in 'Cancelled', 'Completed', or 'Failed' state (returns the current summary). Ownership is verified before cancellation. Requires confirmAction approval — in the confirmation summary include order type, recipient/biller, amount, and the reason.")]
    public async Task<OrderDetailSummary> CancelOrder(
        [Description("The unique identifier (GUID) of the order to cancel")] Guid orderId,
        [Description("Optional reason for cancellation, e.g. 'User requested cancellation' or 'Wrong amount'.")] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var partyId = await ResolveCurrentPartyIdAsync(cancellationToken)
            ?? throw new InvalidOperationException("Current user is not linked to a party and cannot cancel orders.");

        var existing = await _orderService.GetOrderAsync(orderId, cancellationToken);
        if (existing.PayerPartyId != partyId)
        {
            throw new InvalidOperationException($"Order {orderId} not found.");
        }

        var cancelled = await _orderService.CancelOrderAsync(orderId, reason, cancellationToken);

        var firstItem = cancelled.Items.OrderBy(item => item.ItemIndex).FirstOrDefault();
        return new OrderDetailSummary(
            OrderId: cancelled.OrderId,
            OrderType: cancelled.OrderType,
            Status: cancelled.Status,
            OriginCountry: cancelled.OriginCountry,
            OriginCurrency: cancelled.OriginCurrency,
            TotalAmountIn: cancelled.TotalAmountIn,
            TotalFeesAmount: cancelled.TotalFeesAmount,
            TotalAmountOut: cancelled.TotalAmountOut,
            DestinationCurrency: cancelled.DestinationCurrency,
            PurposeCode: cancelled.PurposeCode,
            ItemCount: cancelled.Items.Count,
            PrimaryReceiverName: firstItem?.ReceiverName,
            PrimaryBillerName: firstItem?.BillerName,
            CreatedAt: cancelled.CreatedAt,
            SubmittedAt: cancelled.SubmittedAt);
    }

    private async Task<Guid?> ResolveCurrentPartyIdAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (userId is null)
        {
            return null;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await _financeDbContext.UserParties
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId && link.UserId == userId.Value)
            .OrderByDescending(link => link.Id)
            .Select(link => (Guid?)link.PartyId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

// ── Order summary DTOs ────────────────────────────────────────
//
// Compact shapes for pf_list_orders / pf_get_order / pf_cancel_order.
// The full BillPaymentOrderResponse is large (items, service fields,
// pricing snapshots, party roles, history) — these keep LLM output
// small and force summary-oriented user messages.

public record OrderSummary(
    Guid OrderId,
    string OrderType,
    string Status,
    string OriginCurrency,
    decimal TotalAmountIn,
    string? DestinationCurrency,
    decimal? TotalAmountOut,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record OrderDetailSummary(
    Guid OrderId,
    string OrderType,
    string Status,
    string OriginCountry,
    string OriginCurrency,
    decimal TotalAmountIn,
    decimal TotalFeesAmount,
    decimal TotalAmountOut,
    string? DestinationCurrency,
    string? PurposeCode,
    int ItemCount,
    string? PrimaryReceiverName,
    string? PrimaryBillerName,
    DateTime CreatedAt,
    DateTime? SubmittedAt);
