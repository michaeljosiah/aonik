namespace Aonik.SharedKernel.Abstractions.Finance;

/// <summary>
/// Reads payment-intent history for cross-module consumers (notably PersonalFinance).
/// See <a href="../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
public interface ICustomerPaymentHistoryReader
{
    /// <summary>
    /// Returns payment intents that reference any of the supplied order or invoice
    /// identifiers, scoped to the tenant. Used by the FinancialLifeGraph loader to
    /// hydrate bill-linked payment activity.
    /// </summary>
    Task<IReadOnlyList<PaymentHistoryItem>> GetForOrderOrInvoiceAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> orderIds,
        IReadOnlyCollection<Guid> invoiceIds,
        CancellationToken cancellationToken = default);
}
