using System.Text.Json;
using System.Text.Json.Serialization;

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

public record CreateAccountLinkSessionRequest(
    string Provider,
    string Mode = "connect",
    Guid? ConnectionId = null,
    string? AndroidPackageName = null,
    string? RedirectUri = null,
    string? CountryCode = null,
    string? ClientName = null,
    string? PhoneNumber = null);

public record AccountLinkSessionResponse(
    Guid AccountLinkSessionId,
    string Provider,
    string ProviderDisplayName,
    string Mode,
    string Status,
    Guid? ConnectionId,
    string LaunchToken,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ExchangeAccountLinkSessionRequest(
    Guid AccountLinkSessionId,
    string TemporaryCode);

public record AccountLinkConnectionAccountResponse(
    Guid LinkedAccountId,
    Guid PersonalAccountId,
    string Name,
    string AccountType,
    string? AccountSubtype,
    string Currency,
    string? Last4,
    string Status,
    DateTime? LastSyncedAt,
    string? LastSyncStatus,
    string? LastError,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AccountLinkConnectionResponse(
    Guid ConnectionId,
    string Provider,
    string ProviderDisplayName,
    string ProviderConnectionReference,
    string InstitutionName,
    string? InstitutionReference,
    string Status,
    string ConsentStatus,
    DateTime? LastSyncedAt,
    string? LastSyncStatus,
    string? LastError,
    DateTime? DisconnectedAt,
    IReadOnlyList<AccountLinkConnectionAccountResponse> Accounts,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AccountLinkExchangeResponse(
    Guid AccountLinkSessionId,
    AccountLinkConnectionResponse Connection);

public record AccountLinkActionResponse(
    string Action,
    AccountLinkConnectionResponse Connection);

public record AccountLinkActionRequiredErrorResponse(
    string Error,
    string Message,
    string RequiredAction,
    bool RequiresReconnect,
    Guid ConnectionId,
    string Provider,
    string? ProviderErrorCode);

public record AccountLinkWebhookResponse(string Status);

public record AccountLinkTransactionSyncResponse(
    Guid ConnectionId,
    int TransactionsAdded,
    int TransactionsUpdated,
    int TransactionsRemoved,
    int TransactionsSkipped,
    string SyncStatus,
    string? NextCursor,
    DateTime SyncedAt);

public class PlaidWebhookErrorRequest
{
    [JsonPropertyName("error_type")]
    public string? ErrorType { get; set; }

    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("display_message")]
    public string? DisplayMessage { get; set; }
}

public class PlaidAccountLinkWebhookRequest
{
    [JsonPropertyName("webhook_type")]
    public string WebhookType { get; set; } = string.Empty;

    [JsonPropertyName("webhook_code")]
    public string WebhookCode { get; set; } = string.Empty;

    [JsonPropertyName("item_id")]
    public string? ItemId { get; set; }

    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    [JsonPropertyName("error")]
    public PlaidWebhookErrorRequest? Error { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalData { get; set; }
}

public record AccountLinkSummaryItemResponse(
    Guid PersonalAccountId,
    Guid? ConnectionId,
    Guid? LinkedAccountId,
    string SourceType,
    string Name,
    string AccountType,
    string Currency,
    string? InstitutionName,
    string? AccountSubtype,
    string? Last4,
    string Status,
    string? Provider,
    DateTime? LastSyncedAt,
    string? LastSyncStatus,
    string? LastError,
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
    string TransactionType,
    string? Merchant,
    string? Description,
    string? Category,
    string? SubCategory,
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
    string? SubCategory,
    string? TransactionType,
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

public record FinancialLifeGraphNodeResponse(
    string NodeId,
    string NodeType,
    string DisplayName,
    string SourceType,
    Guid? SourceId,
    string? MetadataJson);

public record FinancialLifeGraphEdgeResponse(
    string FromNodeId,
    string Predicate,
    string ToNodeId,
    string? MetadataJson);

public record FinancialLifeGraphSourceCoverageItemResponse(
    string SourceType,
    int Count);

public record FinancialLifeGraphSummaryResponse(
    int AccountsCount,
    int LinkedAccountsCount,
    int TransactionsCount,
    int BillsCount,
    int GoalsCount,
    int SubscriptionsCount,
    int FundingRelationshipCount,
    int InferredAnnotationCount,
    bool HasHousehold,
    int HouseholdMembersCount,
    int RelatedPartiesCount,
    Guid? PartyId,
    Guid? HouseholdId);

public record FinancialLifeGraphResponse(
    Guid TenantId,
    Guid UserId,
    Guid? HouseholdId,
    DateTime GeneratedAt,
    FinancialLifeGraphSummaryResponse Summary,
    IReadOnlyList<FinancialLifeGraphNodeResponse> Nodes,
    IReadOnlyList<FinancialLifeGraphEdgeResponse> Edges,
    IReadOnlyList<FinancialLifeGraphSourceCoverageItemResponse> SourceCoverage);

public record UpcomingObligationResponse(
    string ItemType,
    Guid SourceId,
    string DisplayName,
    decimal? Amount,
    string Currency,
    DateTime DueDate,
    int DaysUntilDue,
    string Status);

public record CreateFinancialLifeGraphNodeRequest(
    string NodeType,
    string DisplayName,
    string? MetadataJson,
    string? SourceEntity,
    Guid? SourceId,
    Guid? HouseholdId,
    FinancialLifeGraphEntityStatus? Status,
    bool IsInferred,
    Guid? AiRunId);

public record FinancialLifeGraphNodeWriteResponse(
    Guid GraphNodeId,
    string NodeKey);

public record CreateFinancialLifeGraphEdgeRequest(
    string FromNodeKey,
    string Predicate,
    string ToNodeKey,
    string? MetadataJson,
    Guid? HouseholdId,
    FinancialLifeGraphEntityStatus? Status,
    bool IsInferred,
    Guid? AiRunId);

public record FinancialLifeGraphEdgeWriteResponse(Guid GraphEdgeId);

public record ProposeRecurringMerchantGraphAnnotationsRequest(
    Guid AiRunId,
    int MinOccurrences = 3,
    int WithinDays = 90);

public record FinancialLifeGraphInferenceProposalResponse(
    Guid ProposalId,
    Guid GraphNodeId,
    Guid GraphEdgeId,
    string DisplayName,
    string Reasoning,
    int OccurrenceCount,
    FinancialLifeGraphProposalStatus Status);

public record PendingFinancialLifeGraphProposalResponse(
    Guid ProposalId,
    Guid GraphNodeId,
    Guid GraphEdgeId,
    string NodeType,
    string DisplayName,
    string Predicate,
    FinancialLifeGraphProposalStatus Status,
    Guid AiRunId,
    string MetadataJson);

public record RejectFinancialLifeGraphProposalRequest(string? Reason);

public record HouseholdFinanceContextResponse(
    bool HasHousehold,
    Guid? HouseholdId,
    int MemberCount,
    IReadOnlyList<FinancialLifeGraphNodeResponse> Nodes,
    IReadOnlyList<FinancialLifeGraphEdgeResponse> Edges);

public record RelatedPartyFinanceContextItemResponse(
    Guid PartyId,
    string DisplayName,
    string? RelationshipTypeCode,
    string? Notes);

public record RelatedPartyFinanceContextResponse(
    IReadOnlyList<RelatedPartyFinanceContextItemResponse> Parties);
