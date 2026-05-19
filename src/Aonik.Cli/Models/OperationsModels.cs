namespace Aonik.Cli.Models;

public sealed record ScheduledJobSummary(
    string JobName,
    string GroupName,
    string? Description,
    string? CronExpression,
    string Status,
    DateTime? NextFireTimeUtc,
    DateTime? PreviousFireTimeUtc,
    string? DisplayName,
    string? LastOutcome,
    string? LastOutcomeSummary,
    int? LastDurationMs);

public sealed record ScheduledJobListResponse(
    IReadOnlyList<ScheduledJobSummary> Jobs);

public sealed record ScheduledJobActionResponse(
    string JobName,
    string Action,
    bool Success,
    string? Message,
    Guid? CommandId,
    string? CommandStatus);

public sealed record SchedulerHealthResponse(
    string SchedulerName,
    string SchedulerInstanceId,
    bool IsStarted,
    bool InStandbyMode,
    int ThreadPoolSize,
    int ActiveJobCount,
    int TotalJobCount,
    int TotalTriggerCount,
    DateTime RecordedAtUtc);

public sealed record CreateLedgerRequest(string BaseCurrency);

public sealed record LedgerResponse(
    Guid Id,
    string BaseCurrency,
    DateTime CreatedUtc);

public sealed record CreatePaymentIntentRequest(
    decimal Amount,
    string Currency,
    string Reference,
    Guid OrderId,
    Guid? InvoiceId);

public sealed record PaymentIntentResponse(
    Guid Id,
    Guid OrderId,
    Guid? InvoiceId,
    decimal Amount,
    string Currency,
    string Status,
    string Reference,
    DateTime CreatedUtc);

public sealed record InvoiceLineItemResponse(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record InvoiceResponse(
    Guid Id,
    Guid CustomerId,
    string InvoiceNumber,
    string Currency,
    decimal TotalAmount,
    string Status,
    DateTime IssuedUtc,
    DateTime DueUtc,
    List<InvoiceLineItemResponse> LineItems);

public sealed record RunWorkflowOptions(
    string WorkflowName,
    string Input,
    OutputMode OutputMode);

public sealed record JobTriggerOptions(
    string JobName,
    OutputMode OutputMode);

public sealed record CreateLedgerOptions(
    string BaseCurrency,
    OutputMode OutputMode);

public sealed record CreatePaymentIntentOptions(
    decimal Amount,
    string Currency,
    string Reference,
    Guid OrderId,
    Guid? InvoiceId,
    OutputMode OutputMode);

public sealed record ListInvoicesOptions(
    string? Status,
    OutputMode OutputMode);

// ── Invoice lifecycle (Tier 1 additions) ───────────────────────────────

public sealed record CreateInvoiceLineItemInput(
    string Description,
    decimal Quantity,
    decimal UnitPrice);

public sealed record CreateInvoiceRequest(
    Guid CustomerId,
    string InvoiceNumber,
    string Currency,
    DateTime DueUtc,
    List<CreateInvoiceLineItemInput> LineItems);

public sealed record CreateInvoiceOptions(
    Guid CustomerId,
    string InvoiceNumber,
    string Currency,
    DateTime DueUtc,
    string? LinesFile,
    OutputMode OutputMode);

public sealed record InvoiceMutationOptions(
    Guid InvoiceId,
    bool Confirm,
    OutputMode OutputMode);

// ── Orders (Tier 1 additions) ──────────────────────────────────────────

public sealed record PagedResponse<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record OrderListItemResponse(
    Guid OrderId,
    string OrderType,
    string Status,
    Guid? PayerPartyId,
    string PayerName,
    string? OriginCountry,
    string OriginCurrency,
    decimal TotalAmountIn,
    decimal? TotalAmountOut,
    string? DestinationCurrency,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record OrderItemResponse(
    Guid OrderItemId,
    int ItemIndex,
    string ItemType,
    string Status,
    Guid BillerId,
    string BillerName,
    Guid ServiceId,
    string ServiceCode,
    string ServiceName,
    Dictionary<string, string> ServiceFieldValues,
    Guid ReceiverPartyId,
    string ReceiverName,
    string? RelationshipTypeCode,
    decimal AmountIn,
    string CurrencyIn,
    decimal AmountOut,
    string CurrencyOut,
    decimal FeesTotal,
    decimal ExchangeRate,
    Guid? PricingQuoteId,
    DateTime? QuoteExpiresAt,
    bool IsQuoteExpired);

public sealed record BillPaymentOrderResponse(
    Guid OrderId,
    string OrderType,
    string Status,
    Guid PayerPartyId,
    string PayerName,
    string OriginCountry,
    string OriginCurrency,
    decimal TotalAmountIn,
    decimal TotalFeesAmount,
    decimal TotalAmountOut,
    string? DestinationCurrency,
    string? PurposeCode,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    List<OrderItemResponse> Items);

public sealed record CreateReceiverInput(
    string DisplayName,
    string PartyType,
    string? FirstName,
    string? LastName,
    string? Phone,
    string? Email,
    string? CountryCode);

public sealed record CreateBillPaymentItemInput(
    Guid BillerId,
    Guid ServiceId,
    string ServiceCode,
    Dictionary<string, string> ServiceFieldValues,
    Guid? ReceiverPartyId,
    CreateReceiverInput? NewReceiver,
    string? RelationshipTypeCode,
    decimal? OriginAmount,
    decimal? DestinationAmount,
    string DestinationCurrency,
    string DestinationCountry,
    Guid PricingQuoteId,
    string? PurposeCode,
    string? Notes);

public sealed record CreateBillPaymentOrderRequest(
    Guid PayerPartyId,
    string OriginCountry,
    string OriginCurrency,
    string? PurposeCode,
    string? Notes,
    List<CreateBillPaymentItemInput>? Items);

public sealed record ListOrdersOptions(
    int Page,
    int PageSize,
    string? Status,
    string? OrderType,
    string? Search,
    Guid? PayerPartyId,
    OutputMode OutputMode);

public sealed record ListOrdersRequest(
    int PageNumber,
    int PageSize,
    string? Status,
    string? OrderType,
    string? Search,
    Guid? PayerPartyId);

public sealed record CreateBillPaymentOrderOptions(
    Guid PayerPartyId,
    string OriginCountry,
    string OriginCurrency,
    string? PurposeCode,
    string? Notes,
    string? ItemsFile,
    OutputMode OutputMode);

public sealed record SubmitOrderOptions(
    Guid OrderId,
    bool Confirm,
    OutputMode OutputMode);

public sealed record CancelOrderOptions(
    Guid OrderId,
    string? Reason,
    bool Confirm,
    OutputMode OutputMode);

// ── Job detail + control + runs (Tier 1 additions) ─────────────────────

public sealed record ScheduledJobDetailResponse(
    string JobName,
    string GroupName,
    string DisplayName,
    string Description,
    string CronExpression,
    string TimeZoneId,
    string State,
    DateTime? NextFireTimeUtc,
    DateTime? PreviousFireTimeUtc,
    string? LastOutcome,
    string? LastOutcomeSummary,
    int? LastDurationMs,
    DateTime LastSyncedAtUtc,
    string? ConfigurationJson);

public sealed record ScheduledJobRunSummary(
    Guid Id,
    string Outcome,
    string? ErrorMessage,
    int DurationMs,
    string TriggeredBy,
    DateTime FiredAtUtc,
    DateTime CompletedAtUtc,
    string? FireInstanceId);

public sealed record ListJobRunsOptions(
    string JobName,
    int Page,
    int PageSize,
    OutputMode OutputMode);
