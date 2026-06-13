using Aonik.Cli.Models;

namespace Aonik.Cli.Abstractions;

public interface IAonikCliApiClient
{
    Task<PublicAuthProviderSettingsResponse> GetPublicAuthProviderSettingsAsync(
        string baseUrl,
        CancellationToken cancellationToken = default);

    Task<TokenResponseDto> ExchangeTokenAsync(
        string baseUrl,
        TokenRequestDto request,
        CancellationToken cancellationToken = default);

    Task<UserInfoResponseDto> GetUserInfoAsync(
        CliSession session,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentInfo>> ListAgentsAsync(
        CliSession session,
        CancellationToken cancellationToken = default);

    Task<AgentChatResponse> ChatAsync(
        CliSession session,
        ChatRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentStreamEvent>> StreamAgentAsync(
        CliSession session,
        AgentStreamRequest request,
        Func<AgentStreamEvent, Task>? onEvent = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatThreadSummary>> ListThreadsAsync(
        CliSession session,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ChatThreadDetail> GetThreadAsync(
        CliSession session,
        Guid threadId,
        CancellationToken cancellationToken = default);

    Task<WorkflowResponse> RunWorkflowAsync(
        CliSession session,
        WorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<ScheduledJobListResponse> ListScheduledJobsAsync(
        CliSession session,
        CancellationToken cancellationToken = default);

    Task<SchedulerHealthResponse> GetSchedulerHealthAsync(
        CliSession session,
        CancellationToken cancellationToken = default);

    Task<ScheduledJobActionResponse> TriggerScheduledJobAsync(
        CliSession session,
        string jobName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LedgerResponse>> ListLedgersAsync(
        CliSession session,
        CancellationToken cancellationToken = default);

    Task<LedgerResponse> CreateLedgerAsync(
        CliSession session,
        CreateLedgerRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceResponse>> ListInvoicesAsync(
        CliSession session,
        string? status,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse> GetInvoiceAsync(
        CliSession session,
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse> CreateInvoiceAsync(
        CliSession session,
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse> IssueInvoiceAsync(
        CliSession session,
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse> CancelInvoiceAsync(
        CliSession session,
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse> MarkInvoicePaidAsync(
        CliSession session,
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<OrderListItemResponse>> ListOrdersAsync(
        CliSession session,
        ListOrdersRequest request,
        CancellationToken cancellationToken = default);

    Task<BillPaymentOrderResponse> GetOrderAsync(
        CliSession session,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<BillPaymentOrderResponse> CreateBillPaymentOrderAsync(
        CliSession session,
        CreateBillPaymentOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<BillPaymentOrderResponse> SubmitOrderAsync(
        CliSession session,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<BillPaymentOrderResponse> CancelOrderAsync(
        CliSession session,
        Guid orderId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<ScheduledJobDetailResponse> GetScheduledJobDetailAsync(
        CliSession session,
        string jobName,
        CancellationToken cancellationToken = default);

    Task<ScheduledJobActionResponse> PauseScheduledJobAsync(
        CliSession session,
        string jobName,
        CancellationToken cancellationToken = default);

    Task<ScheduledJobActionResponse> ResumeScheduledJobAsync(
        CliSession session,
        string jobName,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ScheduledJobRunSummary>> ListScheduledJobRunsAsync(
        CliSession session,
        string jobName,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PaymentIntentResponse> CreatePaymentIntentAsync(
        CliSession session,
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentIntentResponse> GetPaymentIntentAsync(
        CliSession session,
        Guid paymentIntentId,
        CancellationToken cancellationToken = default);

    Task<PaymentIntentResponse> CapturePaymentAsync(
        CliSession session,
        Guid paymentIntentId,
        CancellationToken cancellationToken = default);

    Task<PaymentIntentResponse> CancelPaymentAsync(
        CliSession session,
        Guid paymentIntentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingFinancialLifeGraphProposalResponse>> ListPendingFinancialLifeGraphProposalsAsync(
        CliSession session,
        CancellationToken cancellationToken = default);

    Task ApproveFinancialLifeGraphProposalAsync(
        CliSession session,
        Guid proposalId,
        CancellationToken cancellationToken = default);

    Task RejectFinancialLifeGraphProposalAsync(
        CliSession session,
        Guid proposalId,
        RejectFinancialLifeGraphProposalRequest request,
        CancellationToken cancellationToken = default);

    // ── CareEntity (Spec 043) ───────────────────────────────────────────

    Task<IReadOnlyList<CareEntityResponse>> ListCareEntitiesAsync(
        CliSession session,
        string? kind,
        string? assetType,
        bool includeArchived,
        CancellationToken cancellationToken = default);

    Task<CareEntityResponse> GetCareEntityAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CareEntityResponse> CreateCareEntityAsync(
        CliSession session,
        CreateCareEntityRequest request,
        CancellationToken cancellationToken = default);

    Task<CareEntityResponse> UpdateCareEntityAsync(
        CliSession session,
        Guid id,
        UpdateCareEntityRequest request,
        CancellationToken cancellationToken = default);

    Task ArchiveCareEntityAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CareEntityProfileResponse> GetCareEntityProfileAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default);

    // ── PaymentLog (Spec 045) ───────────────────────────────────────────

    Task<PaymentLogResponse> CreatePaymentLogAsync(
        CliSession session,
        CreatePaymentLogRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentLogListResponse> ListPaymentLogsAsync(
        CliSession session,
        Guid? careEntityId,
        Guid? commitmentId,
        int? year,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PaymentLogResponse> GetPaymentLogAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PaymentLogResponse> UpdatePaymentLogAsync(
        CliSession session,
        Guid id,
        UpdatePaymentLogRequest request,
        CancellationToken cancellationToken = default);

    Task DeletePaymentLogAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PaymentLogResponse> RestorePaymentLogAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PaymentLogResponse> LinkPaymentLogTransactionAsync(
        CliSession session,
        Guid id,
        Guid transactionId,
        CancellationToken cancellationToken = default);

    Task<PaymentLogResponse> UnlinkPaymentLogTransactionAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<YearSummary> GetPaymentLogYearSummaryAsync(
        CliSession session,
        int year,
        CancellationToken cancellationToken = default);

    // ── Commitment lifecycle (Spec 044) ─────────────────────────────────

    Task<CommitmentDetail> CreateSupportCommitmentAsync(
        CliSession session,
        CreateSupportCommitmentRequest request,
        CancellationToken cancellationToken = default);

    Task<CommitmentDetail> MarkCommitmentDoneAsync(
        CliSession session,
        Guid commitmentId,
        MarkCommitmentDoneRequest request,
        CancellationToken cancellationToken = default);

    Task<CommitmentDetail> SkipCommitmentAsync(
        CliSession session,
        Guid commitmentId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<CommitmentDetail> SnoozeCommitmentAsync(
        CliSession session,
        Guid commitmentId,
        DateTime until,
        CancellationToken cancellationToken = default);

    Task<CommitmentDetail> PauseCommitmentAsync(
        CliSession session,
        Guid commitmentId,
        CancellationToken cancellationToken = default);

    Task<CommitmentDetail> ResumeCommitmentAsync(
        CliSession session,
        Guid commitmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommitmentCycleResponse>> GetCommitmentCyclesAsync(
        CliSession session,
        Guid commitmentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    // ── Documents / Vault (Spec 046) ────────────────────────────────────

    Task<PagedResponse<DocumentListItemDto>> ListDocumentsAsync(
        CliSession session,
        Guid? careEntityId,
        string? documentType,
        int? year,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentLinkDto>> ListDocumentLinksAsync(
        CliSession session,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<DocumentLinkDto> AddDocumentLinkAsync(
        CliSession session,
        Guid documentId,
        AddDocumentLinkRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveDocumentLinkAsync(
        CliSession session,
        Guid documentId,
        Guid linkId,
        CancellationToken cancellationToken = default);

    // ── Circle (Spec 048) ───────────────────────────────────────────────

    Task<CircleGrantResponse> CreateCircleGrantAsync(
        CliSession session,
        CreateCircleGrantRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CircleGrantResponse>> ListCircleGrantsAsync(
        CliSession session,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CircleGrantResponse>> ListCircleSharedWithMeAsync(
        CliSession session,
        CancellationToken cancellationToken = default);

    Task RevokeCircleGrantAsync(
        CliSession session,
        Guid grantId,
        CancellationToken cancellationToken = default);

    Task<CircleInviteResponse> CreateCircleInviteAsync(
        CliSession session,
        CreateCircleInviteRequest request,
        CancellationToken cancellationToken = default);

    Task<CircleGrantResponse> AcceptCircleInviteAsync(
        CliSession session,
        string token,
        CancellationToken cancellationToken = default);

    Task<StatementData> GetSupportStatementAsync(
        CliSession session,
        Guid careEntityId,
        DateTime? from,
        DateTime? to,
        string? preparedFor,
        CancellationToken cancellationToken = default);
}
