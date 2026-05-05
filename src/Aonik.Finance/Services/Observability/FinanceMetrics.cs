using System.Diagnostics.Metrics;

namespace Aonik.Finance.Services.Observability;

/// <summary>
/// Domain-event counters for the Finance module. Emits one counter
/// increment per business-significant write (payment recorded, invoice
/// created, journal entry posted) so dashboards can chart finance
/// activity per-tenant without scraping the database.
/// </summary>
/// <remarks>
/// Meter name "Aonik.Finance" is registered in
/// <c>Aonik.ServiceDefaults.Extensions.AddOpenTelemetry().WithMetrics</c>.
/// All counters tag <c>tenant.id</c> so dashboards can break down per
/// tenant; <c>currency</c> is added where it is meaningful (money
/// movement) so a multi-currency tenant doesn't squash USD and GHS into
/// the same line.
/// </remarks>
public sealed class FinanceMetrics : IDisposable
{
    public const string MeterName = "Aonik.Finance";
    public const string MeterVersion = "1.0.0";

    private readonly Meter _meter;
    private readonly Counter<long> _paymentsRecorded;
    private readonly Counter<long> _invoicesCreated;
    private readonly Counter<long> _ledgerEntriesPosted;

    public FinanceMetrics()
    {
        _meter = new Meter(MeterName, MeterVersion);

        _paymentsRecorded = _meter.CreateCounter<long>(
            name: "aonik.finance.payments.recorded",
            unit: "{payment}",
            description: "Count of Payment rows recorded against an invoice. Tagged with tenant.id, currency, status.");

        _invoicesCreated = _meter.CreateCounter<long>(
            name: "aonik.finance.invoices.created",
            unit: "{invoice}",
            description: "Count of Invoice rows created. Tagged with tenant.id, currency.");

        _ledgerEntriesPosted = _meter.CreateCounter<long>(
            name: "aonik.finance.ledger.entries_posted",
            unit: "{entry}",
            description: "Count of JournalEntry rows posted to a ledger. Tagged with tenant.id, currency.");
    }

    public void RecordPayment(Guid tenantId, string currency, string status) =>
        _paymentsRecorded.Add(1, BuildTags(tenantId, currency, status));

    public void RecordInvoiceCreated(Guid tenantId, string currency) =>
        _invoicesCreated.Add(1, BuildTags(tenantId, currency, status: null));

    public void RecordLedgerEntryPosted(Guid tenantId, string currency) =>
        _ledgerEntriesPosted.Add(1, BuildTags(tenantId, currency, status: null));

    /// <summary>
    /// Build the standard tag set. <c>tenant.id</c> is the breakdown
    /// dimension every dashboard panel uses; <c>currency</c> keeps
    /// multi-currency tenants from collapsing onto one series. Optional
    /// <paramref name="status"/> is included only on payment events.
    /// </summary>
    private static System.Diagnostics.TagList BuildTags(Guid tenantId, string currency, string? status)
    {
        var tags = new System.Diagnostics.TagList
        {
            { "tenant.id", tenantId.ToString() },
            { "currency", string.IsNullOrWhiteSpace(currency) ? "unknown" : currency.ToUpperInvariant() },
        };
        if (!string.IsNullOrWhiteSpace(status))
        {
            tags.Add("status", status);
        }
        return tags;
    }

    public void Dispose() => _meter.Dispose();
}
