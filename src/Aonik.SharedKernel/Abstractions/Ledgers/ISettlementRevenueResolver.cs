namespace Aonik.SharedKernel.Abstractions.Ledgers;

/// <summary>
/// Decides which account a settled invoice line credits, per order type (Spec 088 §9).
/// Module-contributed via <c>IEnumerable&lt;T&gt;</c> DI, keyed by <see cref="OrderTypes"/>.
///
/// Before this, <c>PostInvoiceSettlementAsync</c> credited a hard-coded 4000 unconditionally, so
/// no consumer could recognise revenue anywhere else — and adding a second posting alongside it
/// would have double-recognised the invoice.
///
/// <b>An order type with no registered resolver falls back to 4000 as a single line</b> — today's
/// behaviour exactly, which is what keeps every existing product unaffected by this seam.
/// </summary>
public interface ISettlementRevenueResolver
{
    /// <summary>The order types this resolver claims. Two resolvers claiming one type is a startup failure, not last-writer-wins.</summary>
    IReadOnlyCollection<string> OrderTypes { get; }

    /// <summary>
    /// The account this line credits, and how to tag it. Called once per invoice line, so a
    /// multi-line order can route and dimension each line differently.
    /// </summary>
    SettlementCredit Resolve(string orderType, SettlementLineContext line);
}

/// <summary>One invoice line being settled.</summary>
/// <param name="MetadataJson">
/// The line's own metadata, verbatim. This is how a consumer tells its lines apart: an invoice
/// line carries no link to the order line that produced it, so matching on description or position
/// would be guesswork. The consumer wrote this when it raised the invoice and is the only thing
/// that can interpret it.
/// </param>
public sealed record SettlementLineContext(
    Guid InvoiceId,
    Guid? OrderId,
    Guid InvoiceLineId,
    string Description,
    decimal Amount,
    string Currency,
    string? MetadataJson = null);

/// <summary>
/// Where a settled line's credit goes.
/// </summary>
/// <param name="AccountCode">
/// Resolved within the tenant's ledger, and created if absent — hence <paramref name="AccountName"/>
/// and <paramref name="AccountType"/>.
/// </param>
/// <param name="AccountType">
/// <c>Revenue</c>, <c>Liability</c>, … <b>A credit is not always revenue:</b> cash received for
/// something not yet delivered — prepaid units, deposits, gift balances — belongs in a liability
/// until it is earned. The posting service does not assume.
/// </param>
/// <param name="DimensionsJson">
/// Analytic tags for the credit line, e.g. <c>{"meterCode":"animated-videos"}</c>. Without this a
/// consumer posting many lines into one account can only ever read a tenant-wide aggregate back
/// out of it.
/// </param>
public sealed record SettlementCredit(
    string AccountCode,
    string AccountName,
    string AccountType,
    string? DimensionsJson = null);
