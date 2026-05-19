using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Tests.Support;

internal sealed class FakeAonikCliApiClient : IAonikCliApiClient
{
    public PublicAuthProviderSettingsResponse AuthSettings { get; set; } = new(
        "Auth0",
        new PublicAuth0SettingsResponse("example.auth0.com", "aud", "client-id", "Username-Password-Authentication"),
        new PublicAzureAdSettingsResponse(null, null, null, null));

    public TokenResponseDto TokenResponse { get; set; } = new("token", "refresh", 3600, "Bearer", null);

    public UserInfoResponseDto UserInfoResponse { get; set; } = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "operator@aonik.io",
        "Op",
        "Erator",
        ["PlatformAdmin"],
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        null,
        null,
        null);

    public IReadOnlyList<AgentInfo> Agents { get; set; } =
    [
        new AgentInfo("finance-agent", "Finance operations"),
        new AgentInfo("platform-agent", "Platform operations")
    ];

    public AgentChatResponse ChatResponse { get; set; } = new(
        "Here is the response.",
        "session-123",
        "finance-agent",
        "thread-123",
        "Daily reconciliation");

    public IReadOnlyList<AgentStreamEvent> StreamEvents { get; set; } =
    [
        new AgentStreamEvent("RUN_STARTED", "{\"type\":\"RUN_STARTED\",\"threadId\":\"thread-stream\",\"runId\":\"run-stream\"}"),
        new AgentStreamEvent("TEXT_MESSAGE_CONTENT", "{\"type\":\"TEXT_MESSAGE_CONTENT\",\"delta\":\"stream hello\"}"),
        new AgentStreamEvent("RUN_FINISHED", "{\"type\":\"RUN_FINISHED\",\"threadId\":\"thread-stream\",\"runId\":\"run-stream\"}")
    ];

    public IReadOnlyList<ChatThreadSummary> Threads { get; set; } =
    [
        new ChatThreadSummary(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "Reconciliation",
            "Active",
            "finance-agent",
            DateTime.Parse("2026-04-06T10:00:00Z").ToUniversalTime(),
            2,
            DateTime.Parse("2026-04-06T09:55:00Z").ToUniversalTime())
    ];

    public ChatThreadDetail ThreadDetail { get; set; } = new(
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        "Reconciliation",
        "Active",
        "finance-agent",
        DateTime.Parse("2026-04-06T10:00:00Z").ToUniversalTime(),
        2,
        DateTime.Parse("2026-04-06T09:55:00Z").ToUniversalTime(),
        [
            new ChatThreadMessageDto(
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                "user",
                "Check settlements",
                null,
                null,
                1,
                DateTime.Parse("2026-04-06T09:55:00Z").ToUniversalTime()),
            new ChatThreadMessageDto(
                Guid.Parse("66666666-6666-6666-6666-666666666666"),
                "assistant",
                "Settlements look balanced.",
                "finance-agent",
                null,
                2,
                DateTime.Parse("2026-04-06T10:00:00Z").ToUniversalTime())
        ]);

    public WorkflowResponse WorkflowResponse { get; set; } = new("reconciliation", "Workflow complete", true, null);

    public ScheduledJobListResponse ScheduledJobsResponse { get; set; } = new(
    [
        new ScheduledJobSummary("daily-reconciliation", "finance", "Daily job", "0 0 * * *", "Active", null, null, "Daily reconciliation", "Success", "Completed", 1240)
    ]);

    public SchedulerHealthResponse SchedulerHealthResponse { get; set; } = new(
        "default",
        "instance-1",
        true,
        false,
        10,
        1,
        5,
        6,
        DateTime.Parse("2026-04-06T10:30:00Z").ToUniversalTime());

    public ScheduledJobActionResponse ScheduledJobActionResponse { get; set; } = new(
        "daily-reconciliation",
        "trigger",
        true,
        "Queued",
        Guid.Parse("77777777-7777-7777-7777-777777777777"),
        "Queued");

    public IReadOnlyList<LedgerResponse> Ledgers { get; set; } =
    [
        new LedgerResponse(Guid.Parse("88888888-8888-8888-8888-888888888888"), "USD", DateTime.Parse("2026-04-06T09:00:00Z").ToUniversalTime())
    ];

    public LedgerResponse CreatedLedger { get; set; } = new(
        Guid.Parse("99999999-9999-9999-9999-999999999999"),
        "USD",
        DateTime.Parse("2026-04-06T09:30:00Z").ToUniversalTime());

    public IReadOnlyList<InvoiceResponse> Invoices { get; set; } =
    [
        new InvoiceResponse(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "INV-1001",
            "USD",
            150m,
            "Draft",
            DateTime.Parse("2026-04-06T08:00:00Z").ToUniversalTime(),
            DateTime.Parse("2026-04-20T08:00:00Z").ToUniversalTime(),
            [] )
    ];

    public InvoiceResponse InvoiceDetail { get; set; } = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        "INV-1001",
        "USD",
        150m,
        "Draft",
        DateTime.Parse("2026-04-06T08:00:00Z").ToUniversalTime(),
        DateTime.Parse("2026-04-20T08:00:00Z").ToUniversalTime(),
        [
            new InvoiceLineItemResponse(
                Guid.Parse("12341234-1234-1234-1234-123412341234"),
                "Consulting",
                3m,
                50m,
                150m)
        ]);

    public BillPaymentOrderResponse OrderDetail { get; set; } = new(
        Guid.Parse("aaaa1111-1111-1111-1111-111111111111"),
        "BillPayment",
        "Draft",
        Guid.Parse("bbbb2222-2222-2222-2222-222222222222"),
        "Acme Co",
        "GH",
        "GHS",
        500m,
        12m,
        488m,
        "USD",
        "BILL",
        DateTime.Parse("2026-04-06T08:00:00Z").ToUniversalTime(),
        null,
        []);

    public PagedResponse<OrderListItemResponse> OrdersPage { get; set; } = new(
        [
            new OrderListItemResponse(
                Guid.Parse("aaaa1111-1111-1111-1111-111111111111"),
                "BillPayment",
                "Draft",
                Guid.Parse("bbbb2222-2222-2222-2222-222222222222"),
                "Acme Co",
                "GH",
                "GHS",
                500m,
                488m,
                "USD",
                DateTime.Parse("2026-04-06T08:00:00Z").ToUniversalTime(),
                null)
        ],
        1,
        1,
        20);

    public ScheduledJobDetailResponse JobDetailResponse { get; set; } = new(
        "daily-reconciliation",
        "finance",
        "Daily reconciliation",
        "Reconciles ledgers daily",
        "0 0 * * *",
        "UTC",
        "Active",
        DateTime.Parse("2026-04-07T00:00:00Z").ToUniversalTime(),
        DateTime.Parse("2026-04-06T00:00:00Z").ToUniversalTime(),
        "Success",
        "Completed",
        1240,
        DateTime.Parse("2026-04-06T01:00:00Z").ToUniversalTime(),
        null);

    public PagedResponse<ScheduledJobRunSummary> JobRunsPage { get; set; } = new(
        [
            new ScheduledJobRunSummary(
                Guid.Parse("cccc3333-3333-3333-3333-333333333333"),
                "Success",
                null,
                1240,
                "scheduler",
                DateTime.Parse("2026-04-06T00:00:00Z").ToUniversalTime(),
                DateTime.Parse("2026-04-06T00:00:01Z").ToUniversalTime(),
                "fire-1")
        ],
        1,
        1,
        20);

    public PaymentIntentResponse PaymentIntentResponse { get; set; } = new(
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
        null,
        100m,
        "USD",
        "Pending",
        "PAY-1001",
        DateTime.Parse("2026-04-06T11:00:00Z").ToUniversalTime());

    public IReadOnlyList<PendingFinancialLifeGraphProposalResponse> Proposals { get; set; } =
    [
        new PendingFinancialLifeGraphProposalResponse(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            Guid.Parse("12121212-1212-1212-1212-121212121212"),
            "Merchant",
            "Netflix",
            "recurs_with",
            FinancialLifeGraphProposalStatus.Proposed,
            Guid.Parse("13131313-1313-1313-1313-131313131313"),
            "{}")
    ];

    public Task<PublicAuthProviderSettingsResponse> GetPublicAuthProviderSettingsAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        _ = baseUrl;
        _ = cancellationToken;
        return Task.FromResult(AuthSettings);
    }

    public Task<TokenResponseDto> ExchangeTokenAsync(string baseUrl, TokenRequestDto request, CancellationToken cancellationToken = default)
    {
        _ = baseUrl;
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(TokenResponse);
    }

    public Task<UserInfoResponseDto> GetUserInfoAsync(CliSession session, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = cancellationToken;
        return Task.FromResult(UserInfoResponse);
    }

    public Task<IReadOnlyList<AgentInfo>> ListAgentsAsync(CliSession session, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = cancellationToken;
        return Task.FromResult(Agents);
    }

    public Task<AgentChatResponse> ChatAsync(CliSession session, ChatRequest request, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(ChatResponse);
    }

    public async Task<IReadOnlyList<AgentStreamEvent>> StreamAgentAsync(CliSession session, AgentStreamRequest request, Func<AgentStreamEvent, Task>? onEvent = null, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = request;
        _ = cancellationToken;

        if (onEvent is not null)
        {
            foreach (var streamEvent in StreamEvents)
            {
                await onEvent(streamEvent);
            }
        }

        return StreamEvents;
    }

    public Task<IReadOnlyList<ChatThreadSummary>> ListThreadsAsync(CliSession session, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = page;
        _ = pageSize;
        _ = cancellationToken;
        return Task.FromResult(Threads);
    }

    public Task<ChatThreadDetail> GetThreadAsync(CliSession session, Guid threadId, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = threadId;
        _ = cancellationToken;
        return Task.FromResult(ThreadDetail);
    }

    public Task<WorkflowResponse> RunWorkflowAsync(CliSession session, WorkflowRequest request, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(WorkflowResponse);
    }

    public Task<ScheduledJobListResponse> ListScheduledJobsAsync(CliSession session, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = cancellationToken;
        return Task.FromResult(ScheduledJobsResponse);
    }

    public Task<SchedulerHealthResponse> GetSchedulerHealthAsync(CliSession session, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = cancellationToken;
        return Task.FromResult(SchedulerHealthResponse);
    }

    public Task<ScheduledJobActionResponse> TriggerScheduledJobAsync(CliSession session, string jobName, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = jobName;
        _ = cancellationToken;
        return Task.FromResult(ScheduledJobActionResponse);
    }

    public Task<IReadOnlyList<LedgerResponse>> ListLedgersAsync(CliSession session, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = cancellationToken;
        return Task.FromResult(Ledgers);
    }

    public Task<LedgerResponse> CreateLedgerAsync(CliSession session, CreateLedgerRequest request, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(CreatedLedger);
    }

    public Task<IReadOnlyList<InvoiceResponse>> ListInvoicesAsync(CliSession session, string? status, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = status;
        _ = cancellationToken;
        return Task.FromResult(Invoices);
    }

    public Task<InvoiceResponse> GetInvoiceAsync(CliSession session, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = invoiceId;
        _ = cancellationToken;
        return Task.FromResult(InvoiceDetail);
    }

    public Task<InvoiceResponse> CreateInvoiceAsync(CliSession session, CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(InvoiceDetail);
    }

    public Task<InvoiceResponse> IssueInvoiceAsync(CliSession session, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = invoiceId;
        _ = cancellationToken;
        return Task.FromResult(InvoiceDetail with { Status = "Issued" });
    }

    public Task<InvoiceResponse> CancelInvoiceAsync(CliSession session, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = invoiceId;
        _ = cancellationToken;
        return Task.FromResult(InvoiceDetail with { Status = "Cancelled" });
    }

    public Task<InvoiceResponse> MarkInvoicePaidAsync(CliSession session, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = invoiceId;
        _ = cancellationToken;
        return Task.FromResult(InvoiceDetail with { Status = "Paid" });
    }

    public Task<PagedResponse<OrderListItemResponse>> ListOrdersAsync(CliSession session, ListOrdersRequest request, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(OrdersPage);
    }

    public Task<BillPaymentOrderResponse> GetOrderAsync(CliSession session, Guid orderId, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = orderId;
        _ = cancellationToken;
        return Task.FromResult(OrderDetail);
    }

    public Task<BillPaymentOrderResponse> CreateBillPaymentOrderAsync(CliSession session, CreateBillPaymentOrderRequest request, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(OrderDetail);
    }

    public Task<BillPaymentOrderResponse> SubmitOrderAsync(CliSession session, Guid orderId, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = orderId;
        _ = cancellationToken;
        return Task.FromResult(OrderDetail with { Status = "Submitted", SubmittedAt = DateTime.Parse("2026-04-06T09:00:00Z").ToUniversalTime() });
    }

    public Task<BillPaymentOrderResponse> CancelOrderAsync(CliSession session, Guid orderId, string? reason, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = orderId;
        _ = reason;
        _ = cancellationToken;
        return Task.FromResult(OrderDetail with { Status = "Cancelled" });
    }

    public Task<ScheduledJobDetailResponse> GetScheduledJobDetailAsync(CliSession session, string jobName, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = jobName;
        _ = cancellationToken;
        return Task.FromResult(JobDetailResponse);
    }

    public Task<ScheduledJobActionResponse> PauseScheduledJobAsync(CliSession session, string jobName, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = jobName;
        _ = cancellationToken;
        return Task.FromResult(ScheduledJobActionResponse with { Action = "pause" });
    }

    public Task<ScheduledJobActionResponse> ResumeScheduledJobAsync(CliSession session, string jobName, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = jobName;
        _ = cancellationToken;
        return Task.FromResult(ScheduledJobActionResponse with { Action = "resume" });
    }

    public Task<PagedResponse<ScheduledJobRunSummary>> ListScheduledJobRunsAsync(CliSession session, string jobName, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = jobName;
        _ = pageNumber;
        _ = pageSize;
        _ = cancellationToken;
        return Task.FromResult(JobRunsPage);
    }

    public Task<PaymentIntentResponse> CreatePaymentIntentAsync(CliSession session, CreatePaymentIntentRequest request, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = request;
        _ = cancellationToken;
        return Task.FromResult(PaymentIntentResponse);
    }

    public Task<PaymentIntentResponse> GetPaymentIntentAsync(CliSession session, Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = paymentIntentId;
        _ = cancellationToken;
        return Task.FromResult(PaymentIntentResponse);
    }

    public Task<PaymentIntentResponse> CapturePaymentAsync(CliSession session, Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = paymentIntentId;
        _ = cancellationToken;
        return Task.FromResult(PaymentIntentResponse with { Status = "Captured" });
    }

    public Task<PaymentIntentResponse> CancelPaymentAsync(CliSession session, Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = paymentIntentId;
        _ = cancellationToken;
        return Task.FromResult(PaymentIntentResponse with { Status = "Cancelled" });
    }

    public Task<IReadOnlyList<PendingFinancialLifeGraphProposalResponse>> ListPendingFinancialLifeGraphProposalsAsync(CliSession session, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = cancellationToken;
        return Task.FromResult(Proposals);
    }

    public Task ApproveFinancialLifeGraphProposalAsync(CliSession session, Guid proposalId, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = proposalId;
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    public Task RejectFinancialLifeGraphProposalAsync(CliSession session, Guid proposalId, RejectFinancialLifeGraphProposalRequest request, CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = proposalId;
        _ = request;
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
