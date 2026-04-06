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
}
