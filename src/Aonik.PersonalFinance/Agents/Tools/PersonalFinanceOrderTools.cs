using System.ComponentModel;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Platform;

namespace Aonik.PersonalFinance.Agents.Tools;

/// <summary>
/// Personal-finance order tools — the current user's payment orders (bill
/// payments, transfers, remittances) with ownership-scoped reads and unsettled
/// cancellation (read + mutating). Reads and cancels through the customer-facing
/// <see cref="ICustomerOrderService"/> contract (owner-party scoping enforced
/// server-side) and resolves the caller's party via <see cref="IUserPartyResolver"/>,
/// so the tool takes no dependency on Finance's order internals. Registered by
/// <see cref="PersonalFinanceTools.CreateAll"/>.
/// </summary>
internal sealed class PersonalFinanceOrderTools
{
    private readonly ICustomerOrderService _orderService;
    private readonly IUserPartyResolver _userPartyResolver;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PersonalFinanceOrderTools(
        ICustomerOrderService orderService,
        IUserPartyResolver userPartyResolver,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _orderService = orderService;
        _userPartyResolver = userPartyResolver;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    // ── Order Read Tools ──────────────────────────────────────────

    [Description("Lists the current user's payment orders (bill payments, transfers, remittances), most recent first. Returns compact summaries — do NOT dump full payloads on the user; use this to answer questions like 'what's the status of my recent payments', 'did my transfer go through', or 'show me pending orders'. Filter by status ('Draft', 'Submitted', 'Processing', 'Completed', 'Settled', 'Cancelled', 'Failed') or orderType ('BillPayment', 'Transfer'). Results are automatically scoped to the current user's party — orders belonging to other users in the tenant are never returned.")]
    public async Task<IReadOnlyList<CustomerOrderSummary>> ListOrders(
        [Description("Optional order status filter. Examples: 'Submitted', 'Processing', 'Completed', 'Cancelled', 'Failed'.")] string? status = null,
        [Description("Optional order type filter. Examples: 'BillPayment', 'Transfer'.")] string? orderType = null,
        [Description("Page size (1-50). Defaults to 20.")] int pageSize = 20,
        [Description("Page number (1-based). Defaults to 1.")] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var partyId = await ResolveCurrentPartyIdAsync(cancellationToken);
        if (partyId is null)
        {
            return Array.Empty<CustomerOrderSummary>();
        }

        var limit = Math.Clamp(pageSize, 1, 50);
        var page = pageNumber < 1 ? 1 : pageNumber;

        var result = await _orderService.ListForPartyAsync(
            partyId.Value, status, orderType, page, limit, cancellationToken);

        return result.Items;
    }

    [Description("Retrieves the summary of a single order by its unique identifier — compact shape only (status, amounts, item count, top receiver). Use this when the user asks about a specific order; do not dump the full payload. Ownership is verified: only returns data when the order belongs to the current user's party.")]
    public async Task<CustomerOrderDetail?> GetOrder(
        [Description("The unique identifier (GUID) of the order")] Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var partyId = await ResolveCurrentPartyIdAsync(cancellationToken);
        if (partyId is null)
        {
            return null;
        }

        return await _orderService.GetForPartyAsync(partyId.Value, orderId, cancellationToken);
    }

    // ── Order Mutating Tools ──────────────────────────────────────

    [Description("Cancels a payment order that has not yet settled. No-op for orders already in 'Cancelled', 'Completed', or 'Failed' state (returns the current summary). Ownership is verified before cancellation. Requires confirmAction approval — in the confirmation summary include order type, recipient/biller, amount, and the reason.")]
    public async Task<CustomerOrderDetail> CancelOrder(
        [Description("The unique identifier (GUID) of the order to cancel")] Guid orderId,
        [Description("Optional reason for cancellation, e.g. 'User requested cancellation' or 'Wrong amount'.")] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var partyId = await ResolveCurrentPartyIdAsync(cancellationToken)
            ?? throw new InvalidOperationException("Current user is not linked to a party and cannot cancel orders.");

        return await _orderService.CancelForPartyAsync(partyId, orderId, reason, cancellationToken);
    }

    private async Task<Guid?> ResolveCurrentPartyIdAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (userId is null)
        {
            return null;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await _userPartyResolver.GetPartyIdForUserAsync(tenantId, userId.Value, cancellationToken);
    }
}
