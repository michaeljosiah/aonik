using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ordering;

namespace Aonik.Commerce.Services.Sourcing;

/// <summary>
/// Purchase orders over the shared Order spine (Spec 053 §10–§13). A PO is <em>not</em> a Commerce
/// entity — it is an <c>Order</c> with <c>OrderType = "PurchaseOrder"</c> created via
/// <c>IOrderService.CreateAsync</c>, with <c>OrderItem</c> lines whose <c>ProductId</c>
/// soft-references an ingredient. The spine's <c>TransitionAsync</c> enforces no state machine, so
/// this service enforces the PO's allowed transitions itself, mapped onto the existing
/// <c>OrderStatusCodes</c>: Draft (created) → Pending (submitted to supplier) →
/// Complete (fully received — Spec 054) / Cancelled.
/// </summary>
public interface IPurchaseOrderService
{
    /// <summary>
    /// Creates a Draft PO from explicit lines (§10). Line quantities are in the ingredient's base
    /// unit; a line's unit price is the explicit value when given, else derived from the supplier
    /// catalog (<c>PackPrice / PackSize</c>) — lines with neither are rejected naming the
    /// ingredient. <c>PayerPartyId</c> is null (the tenant is the payer — §11); a
    /// <c>Supplier</c> party role is persisted when the supplier is party-linked; supplier
    /// identity always lands in <c>ProvenanceJson</c>.
    /// </summary>
    Task<OrderDto> CreateAsync(CreatePurchaseOrderCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds a Draft PO from Spec 052 low-stock alerts (§12): the named alert ids, or (auto) every
    /// Open/Acknowledged alert for an ingredient this supplier has a catalog row for. Quantity =
    /// the level's <c>ReorderQuantity</c> when set, else the alert-snapshot shortfall rounded up
    /// to whole packs (min one pack); unit price = <c>PackPrice / PackSize</c>. Flips the source
    /// alerts to <c>Ordered</c> in the same operation.
    /// </summary>
    Task<OrderDto> CreateFromShortfallAsync(CreateFromShortfallCommand command, CancellationToken cancellationToken = default);

    /// <summary>Submits a Draft PO to the supplier — Draft → <c>Pending</c> (reason "Submitted to
    /// supplier"); rejected when the order is not a PO or not currently Draft.</summary>
    Task<OrderDto> SubmitAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Cancels a PO before receipt — Draft/Pending → <c>Cancelled</c>; rejected when the
    /// order is not a PO or already past Pending.</summary>
    Task<OrderDto> CancelAsync(Guid orderId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>Lists purchase orders (newest first), optionally filtered to one status.</summary>
    Task<PagedResult<OrderSummary>> ListAsync(string? status = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
}
