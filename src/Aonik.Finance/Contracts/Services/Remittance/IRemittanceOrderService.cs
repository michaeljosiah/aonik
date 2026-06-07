using Aonik.Finance.Contracts.Models.Remittance;
using Aonik.Finance.Contracts.Services.Partners.Connectors;

namespace Aonik.Finance.Contracts.Services.Remittance;

/// <summary>
/// Payabo B2C remittance orchestration: quote → confirm (lock → debit → connector → transmission) →
/// settle on partner webhook. Remittance is an <c>OrderType</c> business intent executed through the
/// existing payout connector port; it is not a new bounded context. See Spec 036.
/// </summary>
public interface IRemittanceOrderService
{
    /// <summary>Price a corridor and persist a <c>QuoteType = "Remittance"</c> pricing quote.</summary>
    Task<RemittanceQuoteResponse> QuoteAsync(
        RemittanceQuoteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirm a quoted remittance: idempotent on <c>(TenantId, OrderType, IdempotencyKey)</c>. Locks the
    /// quote, posts the ledger debit before any connector call, dispatches the payout, and records the
    /// transmission. Replaying the same <paramref name="idempotencyKey"/> returns the existing order.
    /// </summary>
    Task<RemittanceOrderResponse> ConfirmAsync(
        ConfirmRemittanceRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Read a remittance order scoped to the current tenant and customer; null if not found.</summary>
    Task<RemittanceOrderResponse?> GetAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotently settle (or reverse) a remittance from an inbound partner payout webhook. Non-payout
    /// events are ignored here; settlement/reversal post exactly once via ledger source-type idempotency.
    /// </summary>
    Task ProcessWebhookAsync(
        PartnerWebhookEnvelope envelope,
        CancellationToken cancellationToken = default);
}
