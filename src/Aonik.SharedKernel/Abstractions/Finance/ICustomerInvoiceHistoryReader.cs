namespace Aonik.SharedKernel.Abstractions.Finance;

/// <summary>
/// Reads invoice history for cross-module consumers (notably PersonalFinance).
/// See <a href="../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
public interface ICustomerInvoiceHistoryReader
{
    /// <summary>
    /// Returns invoices matching the supplied identifiers, scoped to the tenant.
    /// Used by the FinancialLifeGraph loader to hydrate bill-linked invoices.
    /// </summary>
    Task<IReadOnlyList<InvoiceHistoryItem>> GetByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> invoiceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether an invoice exists in the given tenant. Used by graph
    /// node-type resolution where only existence (not the full record) matters.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken cancellationToken = default);
}
