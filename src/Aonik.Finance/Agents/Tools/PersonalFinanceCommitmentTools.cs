using System.ComponentModel;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// Personal-finance commitment tools — recurring bills, subscriptions, and debt
/// repayments, plus detected-commitment review (read + mutating). Registered by
/// <see cref="PersonalFinanceTools.CreateAll"/>.
/// </summary>
internal sealed class PersonalFinanceCommitmentTools
{
    private readonly ICommitmentService _commitmentService;

    public PersonalFinanceCommitmentTools(ICommitmentService commitmentService)
    {
        _commitmentService = commitmentService;
    }

    // ── Commitment Read Tools ─────────────────────────────────────

    [Description("Lists all recurring commitments (bills, subscriptions, debt repayments) for the current user. Supports filtering by type ('Bill', 'Subscription', 'DebtRepayment'), status ('Active', 'Paused', 'Cancelled'), and verification status ('Detected', 'Confirmed', 'Rejected'). Returns paginated results with summary totals.")]
    public async Task<CommitmentListResponse> ListCommitments(
        [Description("Filter by commitment type: 'Bill', 'Subscription', or 'DebtRepayment'. Null returns all.")] string? type = null,
        [Description("Filter by lifecycle status: 'Active', 'Paused', 'Cancelled', 'Archived'. Null returns all.")] string? status = null,
        [Description("Filter by verification status: 'Detected', 'Confirmed', 'Rejected'. Null returns all.")] string? verificationStatus = null,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Page size (default: 20, max: 100)")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filter = new CommitmentListFilter(
            Type: type,
            Status: status,
            VerificationStatus: verificationStatus,
            Page: page,
            PageSize: pageSize);
        return await _commitmentService.ListCommitmentsAsync(filter, cancellationToken);
    }

    [Description("Gets full details of a single commitment by ID. Works across all commitment types (bills, subscriptions, debt repayments).")]
    public async Task<CommitmentDetail?> GetCommitment(
        [Description("The unique identifier (GUID) of the commitment")] Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        return await _commitmentService.GetCommitmentAsync(commitmentId, cancellationToken);
    }

    [Description("Lists all detected (unreviewed) commitments that the system found from transaction patterns. These need user review to confirm or reject.")]
    public async Task<IReadOnlyList<CommitmentItem>> ListDetectedCommitments(
        CancellationToken cancellationToken = default)
    {
        return await _commitmentService.ListDetectedAsync(cancellationToken);
    }

    // ── Commitment Mutating Tools ───────────────────────────────

    [Description("Promotes a personal transaction into a tracked recurring commitment. Creates a PersonalRecurringBill, Subscription, or DebtRepayment based on the specified type.")]
    public async Task<CommitmentDetail> CreateCommitmentFromTransaction(
        [Description("The transaction ID (GUID) to promote")] Guid transactionId,
        [Description("Commitment type: 'Bill', 'Subscription', or 'DebtRepayment'")] string commitmentType,
        [Description("Display name for the commitment (e.g. payee or merchant name)")] string displayName,
        [Description("Billing frequency: 'Monthly', 'Weekly', 'Yearly', 'Quarterly'")] string frequency,
        [Description("Next expected due date in UTC")] DateTime nextDueDate,
        [Description("ISO 4217 currency code (e.g. USD, GBP, NGN)")] string currency,
        [Description("Expected recurring amount")] decimal? expectedAmount = null,
        [Description("Whether this commitment is on autopay")] bool autopay = false,
        [Description("Optional: account ID payments come from")] Guid? paidFromAccountId = null,
        [Description("Optional: free-text notes")] string? notes = null,
        [Description("Optional: debt type for DebtRepayment (e.g. 'Mortgage', 'PersonalLoan', 'CreditCardRepayment')")] string? debtType = null,
        [Description("Optional: external account or loan reference")] string? accountReference = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateCommitmentFromTransactionRequest(
            transactionId, commitmentType, displayName, frequency,
            nextDueDate, expectedAmount, currency, paidFromAccountId,
            autopay, notes, debtType, accountReference);
        return await _commitmentService.CreateFromTransactionAsync(request, cancellationToken);
    }

    [Description("Confirms a detected commitment, marking it as verified by the user. Only works on commitments with VerificationStatus = 'Detected'.")]
    public async Task<CommitmentDetail> ConfirmCommitment(
        [Description("The unique identifier (GUID) of the detected commitment to confirm")] Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        return await _commitmentService.ConfirmDetectedAsync(commitmentId, cancellationToken);
    }

    [Description("Rejects a detected commitment, indicating it is not a real recurring obligation. Only works on commitments with VerificationStatus = 'Detected'.")]
    public async Task<string> RejectCommitment(
        [Description("The unique identifier (GUID) of the detected commitment to reject")] Guid commitmentId,
        [Description("Optional reason for rejection")] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        await _commitmentService.RejectDetectedAsync(commitmentId, reason, cancellationToken);
        return $"Commitment {commitmentId} has been rejected.";
    }
}
