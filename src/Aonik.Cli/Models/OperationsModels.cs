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
