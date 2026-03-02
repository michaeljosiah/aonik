namespace Aonik.Finance.Contracts.Models.PersonalFinance;

public record CreateHouseholdRequest(string Name);

public record InviteHouseholdMemberRequest(
    Guid HouseholdId,
    Guid UserId,
    string Role,
    IReadOnlyList<string>? Permissions);

public record HouseholdResponse(
    Guid HouseholdId,
    string Name,
    HouseholdMemberResponse Owner,
    DateTime CreatedAt);

public record HouseholdMemberResponse(
    Guid MemberId,
    Guid HouseholdId,
    Guid UserId,
    string Role,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt);

public record CreatePersonalAccountRequest(
    string Name,
    string AccountType,
    string Currency,
    string? InstitutionName,
    string? ExternalReference,
    string? AccountSubtype,
    string? Last4);

public record UpdatePersonalAccountRequest(
    string Name,
    string AccountType,
    string Currency,
    string? InstitutionName,
    string? ExternalReference,
    string? AccountSubtype,
    string? Last4,
    string Status);

public record PersonalAccountResponse(
    Guid PersonalAccountId,
    Guid UserId,
    Guid? HouseholdId,
    string Name,
    string AccountType,
    string Currency,
    string? InstitutionName,
    string? ExternalReference,
    string Status,
    string? AccountSubtype,
    string? Last4,
    bool IsArchived,
    DateTime? OpenedAt,
    DateTime? ClosedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateManualPersonalTransactionRequest(
    Guid? PersonalAccountId,
    DateTime OccurredAt,
    decimal Amount,
    string Currency,
    string? Merchant,
    string? Description,
    string? Category,
    string? Notes,
    IReadOnlyList<string>? Tags);

public record UpdateManualPersonalTransactionRequest(
    Guid? PersonalAccountId,
    DateTime OccurredAt,
    decimal Amount,
    string Currency,
    string? Merchant,
    string? Description,
    string? Category,
    string? Notes,
    IReadOnlyList<string>? Tags);

public record ListPersonalTransactionsRequest(
    DateTime? From,
    DateTime? To,
    Guid? PersonalAccountId,
    string? Category,
    string? Search,
    int Page = 1,
    int PageSize = 50);

public record PersonalTransactionResponse(
    Guid PersonalTransactionId,
    Guid UserId,
    Guid? PersonalAccountId,
    DateTime OccurredAt,
    decimal Amount,
    string Currency,
    string? Merchant,
    string? Description,
    string? Category,
    decimal Confidence,
    string? CategorisedBy,
    string? ClassificationMethod,
    string? Notes,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record UploadStatementImportRequest(
    Guid PersonalAccountId,
    string FileName,
    string ContentType);

public record StatementImportResponse(
    Guid StatementImportId,
    Guid PersonalAccountId,
    string FileName,
    string Format,
    string Status,
    int RowsTotal,
    int RowsParsed,
    int RowsImported,
    int RowsDuplicate,
    int RowsFailed,
    string? FailureReason,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record StatementImportRowResponse(
    Guid StatementImportRowId,
    Guid StatementImportId,
    int RowNumber,
    string? OccurredAtRaw,
    string? AmountRaw,
    string? DescriptionRaw,
    string? MerchantRaw,
    string? CurrencyRaw,
    DateTime? NormalizedOccurredAt,
    decimal? NormalizedAmount,
    string? NormalizedCurrency,
    string? NormalizedDescription,
    string ParseStatus,
    string? ErrorMessage,
    string? Fingerprint,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record StatementImportApplyResponse(
    Guid StatementImportId,
    int RowsImported,
    int RowsDuplicate,
    int RowsFailed,
    string Status,
    DateTime? CompletedAt);

public record CreateCategorisationRuleRequest(
    string Pattern,
    string Category,
    int Priority,
    string MatchType,
    bool CaseSensitive,
    decimal? MinAmount,
    decimal? MaxAmount,
    Guid? AppliesToAccountId,
    string Scope);

public record UpdateCategorisationRuleRequest(
    string Pattern,
    string Category,
    int Priority,
    bool IsActive,
    string MatchType,
    bool CaseSensitive,
    decimal? MinAmount,
    decimal? MaxAmount,
    Guid? AppliesToAccountId,
    string Scope,
    string ApprovalStatus);

public record CategorisationRuleResponse(
    Guid CategorisationRuleId,
    Guid UserId,
    string Pattern,
    string Category,
    int Priority,
    bool IsActive,
    string MatchType,
    bool CaseSensitive,
    decimal? MinAmount,
    decimal? MaxAmount,
    Guid? AppliesToAccountId,
    bool CreatedFromUserCorrection,
    string Scope,
    string ApprovalStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ClassificationReviewQueueRequest(
    Guid? PersonalAccountId,
    int Page = 1,
    int PageSize = 50);

public record ClassificationReviewItemResponse(
    Guid PersonalTransactionId,
    Guid? PersonalAccountId,
    DateTime OccurredAt,
    decimal Amount,
    string Currency,
    string? Merchant,
    string? Description,
    string? Category,
    decimal Confidence,
    string? CategorisedBy,
    string? ClassificationMethod,
    string ReviewStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record OverrideTransactionClassificationRequest(
    string Category,
    string? Notes,
    bool CreateRuleFromCorrection,
    string? RulePattern,
    int RulePriority = 100,
    string RuleMatchType = "contains");

public record SpendingSummaryResponse(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string Currency,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetAmount,
    int TransactionCount);

public record CategorySpendingItemResponse(
    string Category,
    decimal TotalAmount,
    decimal Percentage,
    int TransactionCount);

public record MerchantSpendingItemResponse(
    string Merchant,
    decimal TotalAmount,
    int TransactionCount);

public record AccountSpendingItemResponse(
    Guid? PersonalAccountId,
    string AccountName,
    decimal TotalAmount,
    int TransactionCount);

public record GeneratePersonalSpendingNarrativeRequest(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    Guid? PersonalAccountId);

public record PersonalSpendingNarrativeInsightResponse(
    Guid InsightId,
    Guid AiRunId,
    string SubjectType,
    Guid SubjectId,
    string Title,
    string Summary,
    DateTime CreatedUtc);
