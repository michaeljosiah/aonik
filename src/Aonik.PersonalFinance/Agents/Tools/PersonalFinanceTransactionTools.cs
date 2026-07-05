using System.ComponentModel;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;

namespace Aonik.PersonalFinance.Agents.Tools;

/// <summary>
/// Personal-finance transaction tools — manual transactions, classification
/// review + rules, statement imports, and receipt attachments (read + mutating).
/// Registered by <see cref="PersonalFinanceTools.CreateAll"/>.
/// </summary>
internal sealed class PersonalFinanceTransactionTools
{
    private readonly IPersonalTransactionService _transactionService;
    private readonly ITransactionClassificationService _classificationService;
    private readonly IStatementImportService _statementImportService;
    private readonly ITransactionAttachmentService _attachmentService;

    public PersonalFinanceTransactionTools(
        IPersonalTransactionService transactionService,
        ITransactionClassificationService classificationService,
        IStatementImportService statementImportService,
        ITransactionAttachmentService attachmentService)
    {
        _transactionService = transactionService;
        _classificationService = classificationService;
        _statementImportService = statementImportService;
        _attachmentService = attachmentService;
    }

    // ── Transaction Read Tools ────────────────────────────────────

    [Description("Lists personal transactions with optional filters. Supports filtering by date range, account, financial context (space), category, and free-text search. Results are paginated.")]
    public async Task<IReadOnlyList<PersonalTransactionResponse>> ListTransactions(
        [Description("Start date filter (UTC, inclusive)")] DateTime? from = null,
        [Description("End date filter (UTC, inclusive)")] DateTime? to = null,
        [Description("Filter by personal account ID")] Guid? personalAccountId = null,
        [Description("Filter by financial context (space) ID")] Guid? financialContextId = null,
        [Description("Filter by category name")] string? category = null,
        [Description("Free-text search in merchant/description")] string? search = null,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Page size (default: 50, max: 100)")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var request = new ListPersonalTransactionsRequest(from, to, personalAccountId, financialContextId, category, search, page, pageSize);
        return await _transactionService.ListTransactionsAsync(request, cancellationToken);
    }

    [Description("Retrieves a personal transaction by its unique identifier. Returns full details including merchant, category, classification info, and notes.")]
    public async Task<PersonalTransactionResponse?> GetTransaction(
        [Description("The unique identifier (GUID) of the personal transaction")] Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _transactionService.GetTransactionAsync(transactionId, cancellationToken);
    }

    // ── Classification Read Tool ──────────────────────────────────

    [Description("Lists transactions in the classification review queue — those with no category assigned or with a pending (unreviewed) suggestion. Each item returns the transaction ID, merchant/description, amount, current category/sub-category (if any), confidence, classification method, and review status. Use this to answer 'what transactions need categorising' or to drive a bulk clean-up flow.")]
    public async Task<IReadOnlyList<ClassificationReviewItemResponse>> ListClassificationReviewQueue(
        [Description("Optional: scope to a specific personal account ID")] Guid? personalAccountId = null,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Page size (default: 50, max: 200)")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var request = new ClassificationReviewQueueRequest(personalAccountId, page, pageSize);
        return await _classificationService.GetReviewQueueAsync(request, cancellationToken);
    }

    // ── Import & Attachment Read Tools ────────────────────────────

    [Description("Lists the user's CSV/OFX statement imports. Each entry shows filename, target account, format, status ('Uploaded', 'Parsed', 'Applied', 'Failed'), and row counts (total/parsed/imported/duplicate/failed). Use this to answer 'how did my import go', 'did my statement upload work', or to find an import ID before previewing or applying it. File uploads happen via the frontend — this tool only lists existing imports.")]
    public async Task<IReadOnlyList<StatementImportResponse>> ListStatementImports(
        CancellationToken cancellationToken = default)
    {
        return await _statementImportService.ListImportsAsync(cancellationToken);
    }

    [Description("Lists the parsed rows of a specific statement import so the user can preview what will be created before applying. Each row returns parse status ('Parsed', 'Duplicate', 'Failed'), raw and normalised values (date, amount, description, merchant, currency), and any error messages. Use this before calling pf_apply_statement_import so the user can confirm the import is sane.")]
    public async Task<IReadOnlyList<StatementImportRowResponse>> ListStatementImportRows(
        [Description("The unique identifier (GUID) of the statement import to preview")] Guid statementImportId,
        CancellationToken cancellationToken = default)
    {
        return await _statementImportService.ListImportRowsAsync(statementImportId, cancellationToken);
    }

    [Description("Lists the receipt/document attachments linked to a specific transaction. Returns file name, mime type, signed URL, optional thumbnail URL, size, and upload time for each attachment. Use this for 'show me the receipt for transaction X' or to check whether a transaction already has proof attached.")]
    public async Task<IReadOnlyList<TransactionAttachmentResponse>> ListTransactionAttachments(
        [Description("The unique identifier (GUID) of the transaction")] Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _attachmentService.GetAttachmentsAsync(transactionId, cancellationToken);
    }

    // ── Transaction Mutating Tools ────────────────────────────────

    [Description("Creates a manual personal transaction. Use this for transactions not imported from a bank (e.g. cash payments, manual adjustments). Requires date, amount, and currency.")]
    public async Task<PersonalTransactionResponse> CreateManualTransaction(
        [Description("Date/time when the transaction occurred (UTC)")] DateTime occurredAt,
        [Description("Transaction amount (positive for income, negative for expense)")] decimal amount,
        [Description("ISO 4217 currency code (e.g. USD, NGN)")] string currency,
        [Description("Optional: personal account ID to associate with")] Guid? personalAccountId = null,
        [Description("Optional: merchant name")] string? merchant = null,
        [Description("Optional: description of the transaction")] string? description = null,
        [Description("Optional: spending category (e.g. 'Groceries', 'Transport', 'Entertainment')")] string? category = null,
        [Description("Optional: additional notes")] string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateManualPersonalTransactionRequest(
            personalAccountId, occurredAt, amount, currency, merchant, description, category, notes, null);
        return await _transactionService.CreateManualTransactionAsync(request, cancellationToken);
    }

    // ── Classification Mutating Tools ─────────────────────────────

    [Description("Manually sets the category for a specific transaction (a confident, user-authored correction — not an AI guess). Clears any pending review status and locks in confidence. Optionally also creates a categorisation rule from this correction so future similar transactions are auto-classified. Requires confirmAction approval.")]
    public async Task<ClassificationReviewItemResponse> OverrideTransactionCategory(
        [Description("The unique identifier (GUID) of the transaction to recategorise")] Guid transactionId,
        [Description("The correct category to apply (e.g. 'Groceries', 'Eating Out', 'Transport')")] string category,
        [Description("Optional free-text notes about the correction")] string? notes = null,
        [Description("If true, also create a rule from this correction so future matching transactions auto-classify (default: false)")] bool createRuleFromCorrection = false,
        CancellationToken cancellationToken = default)
    {
        var request = new OverrideTransactionClassificationRequest(
            Category: category,
            Notes: notes,
            CreateRuleFromCorrection: createRuleFromCorrection,
            RulePattern: null,
            RulePriority: 100,
            RuleMatchType: "contains");
        return await _classificationService.OverrideClassificationAsync(transactionId, request, cancellationToken);
    }

    [Description("Creates a personal categorisation rule that auto-classifies future transactions matching a text pattern. Does NOT retroactively reclassify existing transactions. Requires confirmAction approval.")]
    public async Task<CategorisationRuleResponse> CreateCategorisationRule(
        [Description("Text pattern to match against the transaction's merchant, description, or notes (e.g. 'tesco', 'uber')")] string pattern,
        [Description("Target category to assign when the rule matches (e.g. 'Groceries', 'Transport')")] string category,
        [Description("Match mode: 'contains' (default), 'exact', 'startswith', 'endswith', 'regex', 'amount_range'")] string matchType = "contains",
        [Description("Whether pattern matching is case-sensitive (default: false)")] bool caseSensitive = false,
        [Description("Optional sub-category refinement")] string? subCategory = null,
        [Description("Rule priority — higher values evaluate first (default: 100)")] int priority = 100,
        [Description("Optional: only apply when transaction amount is at or above this value")] decimal? minAmount = null,
        [Description("Optional: only apply when transaction amount is at or below this value")] decimal? maxAmount = null,
        [Description("Optional: scope the rule to a specific personal account ID (null = all accounts)")] Guid? appliesToAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateCategorisationRuleRequest(
            Pattern: pattern,
            Category: category,
            SubCategory: subCategory,
            Priority: priority,
            MatchType: matchType,
            CaseSensitive: caseSensitive,
            MinAmount: minAmount,
            MaxAmount: maxAmount,
            AppliesToAccountId: appliesToAccountId,
            Scope: "User");
        return await _classificationService.CreateRuleAsync(request, cancellationToken);
    }

    // ── Import & Attachment Mutating Tools ────────────────────────

    [Description("Commits the parsed rows of a statement import as real personal transactions. Skips duplicates and failed rows automatically. Only works on imports with status 'Parsed' — for 'Uploaded' or 'Failed' imports, tell the user what's wrong instead. Returns final counts and the applied status. Requires confirmAction approval; before calling, preview rows with pf_list_statement_import_rows and summarise totals/duplicates/failures for the user.")]
    public async Task<StatementImportApplyResponse> ApplyStatementImport(
        [Description("The unique identifier (GUID) of the parsed statement import to commit")] Guid statementImportId,
        CancellationToken cancellationToken = default)
    {
        return await _statementImportService.ApplyImportAsync(statementImportId, cancellationToken);
    }

    [Description("Permanently removes a receipt/document attachment from a transaction. The file is deleted from blob storage. Requires confirmAction approval — name the file and the associated transaction in the confirmation summary.")]
    public async Task<string> DeleteTransactionAttachment(
        [Description("The unique identifier (GUID) of the attachment to delete")] Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        await _attachmentService.DeleteAttachmentAsync(attachmentId, cancellationToken);
        return $"Attachment {attachmentId} has been deleted.";
    }
}
