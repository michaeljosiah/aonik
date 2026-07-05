using System.ComponentModel;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;

namespace Aonik.PersonalFinance.Agents.Tools;

/// <summary>
/// Personal-finance bill tools (read + mutating). Registered by
/// <see cref="PersonalFinanceTools.CreateAll"/>.
/// </summary>
internal sealed class PersonalFinanceBillTools
{
    private readonly IBillService _billService;

    public PersonalFinanceBillTools(IBillService billService)
    {
        _billService = billService;
    }

    // ── Bill Read Tools ───────────────────────────────────────────

    [Description("Lists all bills for the current user. Optionally filter by status (e.g. 'Active', 'Archived').")]
    public async Task<IReadOnlyList<BillResponse>> ListBills(
        [Description("Optional status filter (e.g. 'Active', 'Archived')")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        return await _billService.ListBillsAsync(status, cancellationToken);
    }

    [Description("Retrieves a bill by its unique identifier. Returns full details including payee, frequency, next due date, and linked references.")]
    public async Task<BillResponse?> GetBill(
        [Description("The unique identifier (GUID) of the bill")] Guid billId,
        CancellationToken cancellationToken = default)
    {
        return await _billService.GetBillAsync(billId, cancellationToken);
    }

    [Description("Gets upcoming bills due within a specified number of days. Useful for showing what payments are coming soon.")]
    public async Task<IReadOnlyList<BillResponse>> GetUpcomingBills(
        [Description("Number of days ahead to look for upcoming bills (default: 7)")] int daysAhead = 7,
        CancellationToken cancellationToken = default)
    {
        return await _billService.GetUpcomingBillsAsync(daysAhead, cancellationToken);
    }

    // ── Bill Mutating Tools ───────────────────────────────────────

    [Description("Creates a new recurring bill. Specify the payee, frequency (e.g. 'Monthly', 'Weekly', 'Yearly'), next due date, expected amount, and currency.")]
    public async Task<BillResponse> CreateBill(
        [Description("Name of the payee (e.g. 'Netflix', 'Electricity Company')")] string payee,
        [Description("Billing frequency (e.g. 'Monthly', 'Weekly', 'Biweekly', 'Yearly')")] string frequency,
        [Description("Next due date in UTC")] DateTime nextDueDate,
        [Description("Expected payment amount")] decimal? expectedAmount,
        [Description("ISO 4217 currency code (e.g. USD, NGN)")] string currency,
        [Description("Whether this bill is on autopay")] bool autopay = false,
        [Description("Optional: account ID to pay from")] Guid? paidFromAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateBillRequest(payee, frequency, nextDueDate, expectedAmount, currency, autopay, paidFromAccountId);
        return await _billService.CreateBillAsync(request, cancellationToken);
    }

    [Description("Updates fields on an existing bill. Only the parameters you provide are changed; unspecified fields keep their current values. Use this to reschedule a bill (nextDueDate), adjust an amount, rename a payee, toggle autopay, change the currency, switch the paying account, or change lifecycle status (e.g. 'Active', 'Paid', 'Overdue'). To stop a bill entirely, use pf_archive_bill instead. Requires confirmAction approval.")]
    public async Task<BillResponse> UpdateBill(
        [Description("The unique identifier (GUID) of the bill to update")] Guid billId,
        [Description("Optional: new payee name")] string? payee = null,
        [Description("Optional: new billing frequency (e.g. 'Monthly', 'Weekly', 'Biweekly', 'Yearly')")] string? frequency = null,
        [Description("Optional: new next due date in UTC")] DateTime? nextDueDate = null,
        [Description("Optional: new expected payment amount. Omit to keep the current amount.")] decimal? expectedAmount = null,
        [Description("Optional: new ISO 4217 currency code (e.g. USD, NGN)")] string? currency = null,
        [Description("Optional: enable or disable autopay")] bool? autopay = null,
        [Description("Optional: new account ID to pay from. Omit to keep the current source account.")] Guid? paidFromAccountId = null,
        [Description("Optional: new lifecycle status (e.g. 'Active', 'Paid', 'Overdue'). Use pf_archive_bill to archive a bill rather than setting this to 'Archived'.")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _billService.GetBillAsync(billId, cancellationToken)
            ?? throw new InvalidOperationException($"Bill {billId} not found.");

        var request = new UpdateBillRequest(
            payee ?? existing.Payee,
            frequency ?? existing.Frequency,
            nextDueDate ?? existing.NextDueDate,
            expectedAmount ?? existing.ExpectedAmount,
            currency ?? existing.Currency,
            autopay ?? existing.Autopay,
            paidFromAccountId ?? existing.PaidFromAccountId,
            status ?? existing.Status);

        return await _billService.UpdateBillAsync(billId, request, cancellationToken);
    }

    [Description("Archives a bill, marking it as no longer active. The bill remains in the system for historical reference.")]
    public async Task<string> ArchiveBill(
        [Description("The unique identifier (GUID) of the bill to archive")] Guid billId,
        CancellationToken cancellationToken = default)
    {
        await _billService.ArchiveBillAsync(billId, cancellationToken);
        return $"Bill {billId} has been archived successfully.";
    }
}
