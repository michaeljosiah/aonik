using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Infrastructure;

public sealed class AonikCliApiClient : IAonikCliApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;

    public AonikCliApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<PublicAuthProviderSettingsResponse> GetPublicAuthProviderSettingsAsync(
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PublicAuthProviderSettingsResponse>(
            baseUrl,
            HttpMethod.Get,
            "/v1/settings/auth-provider",
            session: null,
            body: null,
            cancellationToken);
    }

    public Task<TokenResponseDto> ExchangeTokenAsync(
        string baseUrl,
        TokenRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<TokenResponseDto>(
            baseUrl,
            HttpMethod.Post,
            "/auth/token",
            session: null,
            request,
            cancellationToken);
    }

    public Task<UserInfoResponseDto> GetUserInfoAsync(
        CliSession session,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<UserInfoResponseDto>(
            session.BaseUrl,
            HttpMethod.Get,
            "/identity/userinfo",
            session,
            body: null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AgentInfo>> ListAgentsAsync(
        CliSession session,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ListAgentsResponse>(
            session.BaseUrl,
            HttpMethod.Get,
            "/ai/agents",
            session,
            body: null,
            cancellationToken);

        return response.Agents;
    }

    public Task<AgentChatResponse> ChatAsync(
        CliSession session,
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<AgentChatResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            "/ai/chat",
            session,
            request,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AgentStreamEvent>> StreamAgentAsync(
        CliSession session,
        AgentStreamRequest request,
        Func<AgentStreamEvent, Task>? onEvent = null,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri(session.BaseUrl, "/ai/agui"));

        ApplySessionHeaders(httpRequest, session);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        httpRequest.Content = JsonContent.Create(
            new
            {
                threadId = request.ThreadId,
                runId = request.RunId,
                agentId = request.AgentId,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = request.Message
                    }
                }
            },
            options: JsonOptions);

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await BuildErrorAsync(response, cancellationToken);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var events = new List<AgentStreamEvent>();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            var json = line[6..].Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var eventType = root.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString() ?? "UNKNOWN"
                : "UNKNOWN";
            var customName = root.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;

            var streamEvent = new AgentStreamEvent(eventType, json, customName);
            events.Add(streamEvent);

            if (onEvent is not null)
            {
                await onEvent(streamEvent);
            }
        }

        return events;
    }

    public async Task<IReadOnlyList<ChatThreadSummary>> ListThreadsAsync(
        CliSession session,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ListChatThreadsResponse>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/ai/threads?page={page}&pageSize={pageSize}",
            session,
            body: null,
            cancellationToken);

        return response.Threads;
    }

    public Task<ChatThreadDetail> GetThreadAsync(
        CliSession session,
        Guid threadId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ChatThreadDetail>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/ai/threads/{threadId:D}",
            session,
            body: null,
            cancellationToken);
    }

    public Task<WorkflowResponse> RunWorkflowAsync(
        CliSession session,
        WorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<WorkflowResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            "/ai/workflows/run",
            session,
            request,
            cancellationToken);
    }

    public Task<ScheduledJobListResponse> ListScheduledJobsAsync(
        CliSession session,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ScheduledJobListResponse>(
            session.BaseUrl,
            HttpMethod.Get,
            "/admin/jobs/scheduled",
            session,
            body: null,
            cancellationToken);
    }

    public Task<SchedulerHealthResponse> GetSchedulerHealthAsync(
        CliSession session,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<SchedulerHealthResponse>(
            session.BaseUrl,
            HttpMethod.Get,
            "/admin/scheduler/health",
            session,
            body: null,
            cancellationToken);
    }

    public Task<ScheduledJobActionResponse> TriggerScheduledJobAsync(
        CliSession session,
        string jobName,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ScheduledJobActionResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            $"/admin/jobs/scheduled/{Uri.EscapeDataString(jobName)}/trigger",
            session,
            body: new { },
            cancellationToken);
    }

    public async Task<IReadOnlyList<LedgerResponse>> ListLedgersAsync(
        CliSession session,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<List<LedgerResponse>>(
            session.BaseUrl,
            HttpMethod.Get,
            "/ledger",
            session,
            body: null,
            cancellationToken);
    }

    public Task<LedgerResponse> CreateLedgerAsync(
        CliSession session,
        CreateLedgerRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<LedgerResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            "/ledger",
            session,
            request,
            cancellationToken);
    }

    public async Task<IReadOnlyList<InvoiceResponse>> ListInvoicesAsync(
        CliSession session,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(status)
            ? "/billing/invoices"
            : $"/billing/invoices?status={Uri.EscapeDataString(status)}";

        return await SendAsync<List<InvoiceResponse>>(
            session.BaseUrl,
            HttpMethod.Get,
            path,
            session,
            body: null,
            cancellationToken);
    }

    public Task<InvoiceResponse> GetInvoiceAsync(
        CliSession session,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<InvoiceResponse>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/billing/invoices/{invoiceId:D}",
            session,
            body: null,
            cancellationToken);
    }

    public Task<InvoiceResponse> CreateInvoiceAsync(
        CliSession session,
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<InvoiceResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            "/billing/invoices",
            session,
            request,
            cancellationToken);
    }

    public Task<InvoiceResponse> IssueInvoiceAsync(
        CliSession session,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<InvoiceResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            $"/billing/invoices/{invoiceId:D}/issue",
            session,
            body: new { },
            cancellationToken);
    }

    public Task<InvoiceResponse> CancelInvoiceAsync(
        CliSession session,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<InvoiceResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            $"/billing/invoices/{invoiceId:D}/cancel",
            session,
            body: new { },
            cancellationToken);
    }

    public Task<InvoiceResponse> MarkInvoicePaidAsync(
        CliSession session,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<InvoiceResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            $"/billing/invoices/{invoiceId:D}/mark-paid",
            session,
            body: new { },
            cancellationToken);
    }

    public Task<PagedResponse<OrderListItemResponse>> ListOrdersAsync(
        CliSession session,
        ListOrdersRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"pageNumber={request.PageNumber}",
            $"pageSize={request.PageSize}"
        };
        if (!string.IsNullOrWhiteSpace(request.Status)) query.Add($"status={Uri.EscapeDataString(request.Status)}");
        if (!string.IsNullOrWhiteSpace(request.OrderType)) query.Add($"orderType={Uri.EscapeDataString(request.OrderType)}");
        if (!string.IsNullOrWhiteSpace(request.Search)) query.Add($"search={Uri.EscapeDataString(request.Search)}");
        if (request.PayerPartyId.HasValue) query.Add($"payerPartyId={request.PayerPartyId.Value:D}");

        return SendAsync<PagedResponse<OrderListItemResponse>>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/orders?{string.Join('&', query)}",
            session,
            body: null,
            cancellationToken);
    }

    public Task<BillPaymentOrderResponse> GetOrderAsync(
        CliSession session,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<BillPaymentOrderResponse>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/orders/{orderId:D}",
            session,
            body: null,
            cancellationToken);
    }

    public Task<BillPaymentOrderResponse> CreateBillPaymentOrderAsync(
        CliSession session,
        CreateBillPaymentOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<BillPaymentOrderResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            "/orders/bill-payments",
            session,
            request,
            cancellationToken);
    }

    public Task<BillPaymentOrderResponse> SubmitOrderAsync(
        CliSession session,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<BillPaymentOrderResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            $"/orders/{orderId:D}/submit",
            session,
            body: new { },
            cancellationToken);
    }

    public Task<BillPaymentOrderResponse> CancelOrderAsync(
        CliSession session,
        Guid orderId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<BillPaymentOrderResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            $"/orders/{orderId:D}/cancel",
            session,
            new { reason },
            cancellationToken);
    }

    public Task<ScheduledJobDetailResponse> GetScheduledJobDetailAsync(
        CliSession session,
        string jobName,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ScheduledJobDetailResponse>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/admin/jobs/scheduled/{Uri.EscapeDataString(jobName)}",
            session,
            body: null,
            cancellationToken);
    }

    public Task<ScheduledJobActionResponse> PauseScheduledJobAsync(
        CliSession session,
        string jobName,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ScheduledJobActionResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            $"/admin/jobs/scheduled/{Uri.EscapeDataString(jobName)}/pause",
            session,
            body: new { },
            cancellationToken);
    }

    public Task<ScheduledJobActionResponse> ResumeScheduledJobAsync(
        CliSession session,
        string jobName,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<ScheduledJobActionResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            $"/admin/jobs/scheduled/{Uri.EscapeDataString(jobName)}/resume",
            session,
            body: new { },
            cancellationToken);
    }

    public Task<PagedResponse<ScheduledJobRunSummary>> ListScheduledJobRunsAsync(
        CliSession session,
        string jobName,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PagedResponse<ScheduledJobRunSummary>>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/admin/jobs/scheduled/{Uri.EscapeDataString(jobName)}/runs?pageNumber={pageNumber}&pageSize={pageSize}",
            session,
            body: null,
            cancellationToken);
    }

    public Task<PaymentIntentResponse> CreatePaymentIntentAsync(
        CliSession session,
        CreatePaymentIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PaymentIntentResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            "/payments/intents",
            session,
            request,
            cancellationToken);
    }

    public Task<PaymentIntentResponse> GetPaymentIntentAsync(
        CliSession session,
        Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PaymentIntentResponse>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/payments/intents/{paymentIntentId:D}",
            session,
            body: null,
            cancellationToken);
    }

    public Task<PaymentIntentResponse> CapturePaymentAsync(
        CliSession session,
        Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PaymentIntentResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            $"/payments/intents/{paymentIntentId:D}/capture",
            session,
            body: new { },
            cancellationToken);
    }

    public Task<PaymentIntentResponse> CancelPaymentAsync(
        CliSession session,
        Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PaymentIntentResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            $"/payments/intents/{paymentIntentId:D}/cancel",
            session,
            body: new { },
            cancellationToken);
    }

    public async Task<IReadOnlyList<PendingFinancialLifeGraphProposalResponse>> ListPendingFinancialLifeGraphProposalsAsync(
        CliSession session,
        CancellationToken cancellationToken = default)
    {
        return await SendAsync<List<PendingFinancialLifeGraphProposalResponse>>(
            session.BaseUrl,
            HttpMethod.Get,
            "/personal-finance/graph/proposals/pending",
            session,
            body: null,
            cancellationToken);
    }

    public async Task ApproveFinancialLifeGraphProposalAsync(
        CliSession session,
        Guid proposalId,
        CancellationToken cancellationToken = default)
    {
        // Spec 030: FLG proposals now flow through the generic dispatcher.
        // The endpoint returns 200 with a ProposalDetailResponse body; the CLI
        // discards the body since the existing API surface returned NoContent.
        await SendNoContentAsync(
            session.BaseUrl,
            HttpMethod.Post,
            $"/ai/proposals/{proposalId:D}/approve",
            session,
            body: new { },
            cancellationToken);
    }

    public async Task RejectFinancialLifeGraphProposalAsync(
        CliSession session,
        Guid proposalId,
        RejectFinancialLifeGraphProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        // Spec 030: the generic dismiss endpoint does not accept a structured
        // reason in v1 (the schema only carries Approve/Dismiss). The legacy
        // `request.Reason` is dropped — a future schema change will reinstate
        // structured rejection metadata (see spec 030 §5.6 / §6.4).
        _ = request;
        await SendNoContentAsync(
            session.BaseUrl,
            HttpMethod.Post,
            $"/ai/proposals/{proposalId:D}/dismiss",
            session,
            body: new { },
            cancellationToken);
    }

    public Task<PaymentLogResponse> CreatePaymentLogAsync(
        CliSession session,
        CreatePaymentLogRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PaymentLogResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            "/personal-finance/payment-logs",
            session,
            request,
            cancellationToken);
    }

    public async Task<PaymentLogListResponse> ListPaymentLogsAsync(
        CliSession session,
        Guid? careEntityId,
        Guid? commitmentId,
        int? year,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={(pageSize is > 0 and <= 100 ? pageSize : 20)}"
        };
        if (careEntityId.HasValue) query.Add($"careEntityId={careEntityId.Value:D}");
        if (commitmentId.HasValue) query.Add($"commitmentId={commitmentId.Value:D}");
        if (year.HasValue) query.Add($"year={year.Value}");

        return await SendAsync<PaymentLogListResponse>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/personal-finance/payment-logs?{string.Join('&', query)}",
            session,
            body: null,
            cancellationToken);
    }

    public Task<PaymentLogResponse> GetPaymentLogAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PaymentLogResponse>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/personal-finance/payment-logs/{id:D}",
            session,
            body: null,
            cancellationToken);
    }

    public Task<PaymentLogResponse> UpdatePaymentLogAsync(
        CliSession session,
        Guid id,
        UpdatePaymentLogRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PaymentLogResponse>(
            session.BaseUrl,
            HttpMethod.Put,
            $"/personal-finance/payment-logs/{id:D}",
            session,
            request,
            cancellationToken);
    }

    public Task DeletePaymentLogAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return SendNoContentAsync(
            session.BaseUrl,
            HttpMethod.Delete,
            $"/personal-finance/payment-logs/{id:D}",
            session,
            body: null,
            cancellationToken);
    }

    public Task<PaymentLogResponse> RestorePaymentLogAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PaymentLogResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            $"/personal-finance/payment-logs/{id:D}/restore",
            session,
            body: new { },
            cancellationToken);
    }

    public Task<PaymentLogResponse> LinkPaymentLogTransactionAsync(
        CliSession session,
        Guid id,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PaymentLogResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            $"/personal-finance/payment-logs/{id:D}/transaction-link",
            session,
            new { transactionId },
            cancellationToken);
    }

    public Task<PaymentLogResponse> UnlinkPaymentLogTransactionAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PaymentLogResponse>(
            session.BaseUrl,
            HttpMethod.Delete,
            $"/personal-finance/payment-logs/{id:D}/transaction-link",
            session,
            body: null,
            cancellationToken);
    }

    public Task<YearSummary> GetPaymentLogYearSummaryAsync(
        CliSession session,
        int year,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<YearSummary>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/personal-finance/summary/year?year={year}",
            session,
            body: null,
            cancellationToken);
    }

    public async Task<PagedResponse<DocumentListItemDto>> ListDocumentsAsync(
        CliSession session,
        Guid? careEntityId,
        string? documentType,
        int? year,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"pageNumber={Math.Max(1, page)}",
            $"pageSize={(pageSize is > 0 and <= 100 ? pageSize : 20)}"
        };
        if (careEntityId.HasValue) query.Add($"careEntityId={careEntityId.Value:D}");
        if (!string.IsNullOrWhiteSpace(documentType)) query.Add($"documentType={Uri.EscapeDataString(documentType)}");
        if (year.HasValue) query.Add($"year={year.Value}");

        return await SendAsync<PagedResponse<DocumentListItemDto>>(
            session.BaseUrl, HttpMethod.Get, $"/documents?{string.Join('&', query)}", session, body: null, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentLinkDto>> ListDocumentLinksAsync(
        CliSession session,
        Guid documentId,
        CancellationToken cancellationToken = default)
        => await SendAsync<List<DocumentLinkDto>>(
            session.BaseUrl, HttpMethod.Get, $"/documents/{documentId:D}/links", session, body: null, cancellationToken);

    public Task<DocumentLinkDto> AddDocumentLinkAsync(
        CliSession session,
        Guid documentId,
        AddDocumentLinkRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<DocumentLinkDto>(
            session.BaseUrl, HttpMethod.Post, $"/documents/{documentId:D}/links", session, request, cancellationToken);

    public Task RemoveDocumentLinkAsync(
        CliSession session,
        Guid documentId,
        Guid linkId,
        CancellationToken cancellationToken = default)
        => SendNoContentAsync(
            session.BaseUrl, HttpMethod.Delete, $"/documents/{documentId:D}/links/{linkId:D}", session, body: null, cancellationToken);

    public Task<CircleGrantResponse> CreateCircleGrantAsync(
        CliSession session,
        CreateCircleGrantRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<CircleGrantResponse>(
            session.BaseUrl, HttpMethod.Post, "/personal-finance/circle/grants", session, request, cancellationToken);

    public async Task<IReadOnlyList<CircleGrantResponse>> ListCircleGrantsAsync(
        CliSession session,
        CancellationToken cancellationToken = default)
        => await SendAsync<List<CircleGrantResponse>>(
            session.BaseUrl, HttpMethod.Get, "/personal-finance/circle/grants", session, body: null, cancellationToken);

    public async Task<IReadOnlyList<CircleGrantResponse>> ListCircleSharedWithMeAsync(
        CliSession session,
        CancellationToken cancellationToken = default)
        => await SendAsync<List<CircleGrantResponse>>(
            session.BaseUrl, HttpMethod.Get, "/personal-finance/circle/shared", session, body: null, cancellationToken);

    public Task RevokeCircleGrantAsync(
        CliSession session,
        Guid grantId,
        CancellationToken cancellationToken = default)
        => SendNoContentAsync(
            session.BaseUrl, HttpMethod.Delete, $"/personal-finance/circle/grants/{grantId:D}", session, body: null, cancellationToken);

    public Task<CircleInviteResponse> CreateCircleInviteAsync(
        CliSession session,
        CreateCircleInviteRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<CircleInviteResponse>(
            session.BaseUrl, HttpMethod.Post, "/personal-finance/circle/invites", session, request, cancellationToken);

    public Task<CircleGrantResponse> AcceptCircleInviteAsync(
        CliSession session,
        string token,
        CancellationToken cancellationToken = default)
        => SendAsync<CircleGrantResponse>(
            session.BaseUrl, HttpMethod.Post, "/personal-finance/circle/invites/accept", session, new { token }, cancellationToken);

    public Task<StatementData> GetSupportStatementAsync(
        CliSession session,
        Guid careEntityId,
        DateTime? from,
        DateTime? to,
        string? preparedFor,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (from.HasValue) query.Add($"from={from.Value:yyyy-MM-dd}");
        if (to.HasValue) query.Add($"to={to.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(preparedFor)) query.Add($"preparedFor={Uri.EscapeDataString(preparedFor)}");

        var path = $"/personal-finance/care-entities/{careEntityId:D}/statement"
            + (query.Count > 0 ? $"?{string.Join('&', query)}" : string.Empty);

        return SendAsync<StatementData>(session.BaseUrl, HttpMethod.Get, path, session, body: null, cancellationToken);
    }

    public Task<CaptureParseResponse> ParseCaptureAsync(
        CliSession session,
        CaptureParseRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<CaptureParseResponse>(
            session.BaseUrl, HttpMethod.Post, "/ai/capture/parse", session, request, cancellationToken);

    public Task<CommitmentDetail> CreateSupportCommitmentAsync(
        CliSession session,
        CreateSupportCommitmentRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<CommitmentDetail>(
            session.BaseUrl, HttpMethod.Post, "/personal-finance/commitments", session, request, cancellationToken);

    public Task<CommitmentDetail> MarkCommitmentDoneAsync(
        CliSession session,
        Guid commitmentId,
        MarkCommitmentDoneRequest request,
        CancellationToken cancellationToken = default)
        => SendAsync<CommitmentDetail>(
            session.BaseUrl, HttpMethod.Post, $"/personal-finance/commitments/{commitmentId:D}/done", session, request, cancellationToken);

    public Task<CommitmentDetail> SkipCommitmentAsync(
        CliSession session,
        Guid commitmentId,
        string? reason,
        CancellationToken cancellationToken = default)
        => SendAsync<CommitmentDetail>(
            session.BaseUrl, HttpMethod.Post, $"/personal-finance/commitments/{commitmentId:D}/skip", session, new { reason }, cancellationToken);

    public Task<CommitmentDetail> SnoozeCommitmentAsync(
        CliSession session,
        Guid commitmentId,
        DateTime until,
        CancellationToken cancellationToken = default)
        => SendAsync<CommitmentDetail>(
            session.BaseUrl, HttpMethod.Post, $"/personal-finance/commitments/{commitmentId:D}/snooze", session, new { until }, cancellationToken);

    public Task<CommitmentDetail> PauseCommitmentAsync(
        CliSession session,
        Guid commitmentId,
        CancellationToken cancellationToken = default)
        => SendAsync<CommitmentDetail>(
            session.BaseUrl, HttpMethod.Post, $"/personal-finance/commitments/{commitmentId:D}/pause", session, new { }, cancellationToken);

    public Task<CommitmentDetail> ResumeCommitmentAsync(
        CliSession session,
        Guid commitmentId,
        CancellationToken cancellationToken = default)
        => SendAsync<CommitmentDetail>(
            session.BaseUrl, HttpMethod.Post, $"/personal-finance/commitments/{commitmentId:D}/resume", session, new { }, cancellationToken);

    public async Task<IReadOnlyList<CommitmentCycleResponse>> GetCommitmentCyclesAsync(
        CliSession session,
        Guid commitmentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
        => await SendAsync<List<CommitmentCycleResponse>>(
            session.BaseUrl, HttpMethod.Get,
            $"/personal-finance/commitments/{commitmentId:D}/cycles?page={page}&pageSize={pageSize}",
            session, body: null, cancellationToken);

    public async Task<IReadOnlyList<CareEntityResponse>> ListCareEntitiesAsync(
        CliSession session,
        string? kind,
        string? assetType,
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(kind)) query.Add($"kind={Uri.EscapeDataString(kind)}");
        if (!string.IsNullOrWhiteSpace(assetType)) query.Add($"assetType={Uri.EscapeDataString(assetType)}");
        if (includeArchived) query.Add("includeArchived=true");

        var path = query.Count == 0
            ? "/personal-finance/care-entities"
            : $"/personal-finance/care-entities?{string.Join('&', query)}";

        return await SendAsync<List<CareEntityResponse>>(
            session.BaseUrl,
            HttpMethod.Get,
            path,
            session,
            body: null,
            cancellationToken);
    }

    public Task<CareEntityResponse> GetCareEntityAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<CareEntityResponse>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/personal-finance/care-entities/{id:D}",
            session,
            body: null,
            cancellationToken);
    }

    public Task<CareEntityResponse> CreateCareEntityAsync(
        CliSession session,
        CreateCareEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<CareEntityResponse>(
            session.BaseUrl,
            HttpMethod.Post,
            "/personal-finance/care-entities",
            session,
            request,
            cancellationToken);
    }

    public Task<CareEntityResponse> UpdateCareEntityAsync(
        CliSession session,
        Guid id,
        UpdateCareEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<CareEntityResponse>(
            session.BaseUrl,
            HttpMethod.Put,
            $"/personal-finance/care-entities/{id:D}",
            session,
            request,
            cancellationToken);
    }

    public Task ArchiveCareEntityAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return SendNoContentAsync(
            session.BaseUrl,
            HttpMethod.Post,
            $"/personal-finance/care-entities/{id:D}/archive",
            session,
            body: new { },
            cancellationToken);
    }

    public Task<CareEntityProfileResponse> GetCareEntityProfileAsync(
        CliSession session,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<CareEntityProfileResponse>(
            session.BaseUrl,
            HttpMethod.Get,
            $"/personal-finance/care-entities/{id:D}/profile",
            session,
            body: null,
            cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse>(
        string baseUrl,
        HttpMethod method,
        string path,
        CliSession? session,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildUri(baseUrl, path));
        ApplySessionHeaders(request, session);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await BuildErrorAsync(response, cancellationToken);
        }

        var payload = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
        if (payload is null)
        {
            throw new AonikCliException($"AONIK API returned an empty response for {method} {path}.");
        }

        return payload;
    }

    private async Task SendNoContentAsync(
        string baseUrl,
        HttpMethod method,
        string path,
        CliSession? session,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildUri(baseUrl, path));
        ApplySessionHeaders(request, session);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await BuildErrorAsync(response, cancellationToken);
        }
    }

    // ── Commerce storefront options (Spec 066, anonymous) ───────────────

    public Task<IReadOnlyList<CliOptionGroup>> GetOptionCatalogueAsync(
        StorefrontTarget target,
        CancellationToken cancellationToken = default)
        => SendAnonymousAsync<IReadOnlyList<CliOptionGroup>>(
            target, HttpMethod.Get, "/commerce/catalog/options", body: null, cancellationToken);

    public Task<CliStorefrontProduct> GetStorefrontProductAsync(
        StorefrontTarget target,
        string slug,
        CancellationToken cancellationToken = default)
        => SendAnonymousAsync<CliStorefrontProduct>(
            target, HttpMethod.Get, $"/commerce/catalog/products/{Uri.EscapeDataString(slug)}", body: null, cancellationToken);

    public Task<CliSelectionQuote> GetSelectionQuoteAsync(
        StorefrontTarget target,
        string slug,
        IReadOnlyDictionary<string, object> selection,
        string? currency,
        CancellationToken cancellationToken = default)
        => SendAnonymousAsync<CliSelectionQuote>(
            target,
            HttpMethod.Post,
            $"/commerce/catalog/products/{Uri.EscapeDataString(slug)}/selection-quote",
            new { selection, currency },
            cancellationToken);

    /// <summary>
    /// Issues a tenant-scoped request with no bearer token. The Spec 066 storefront endpoints are
    /// anonymous by design, so this deliberately does not require (or create) a CLI session.
    /// </summary>
    private async Task<T> SendAnonymousAsync<T>(
        StorefrontTarget target,
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, BuildUri(target.BaseUrl, path));
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", target.TenantId.ToString("D"));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await BuildErrorAsync(response, cancellationToken);
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new AonikCliException($"AONIK API returned an empty body for {path}.");
    }

    private static void ApplySessionHeaders(HttpRequestMessage request, CliSession? session)
    {
        if (session is null)
        {
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        if (session.TenantId.HasValue)
        {
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", session.TenantId.Value.ToString("D"));
        }
    }

    private static Uri BuildUri(string baseUrl, string path)
    {
        var normalizedBaseUrl = baseUrl.Trim().TrimEnd('/');
        return new Uri($"{normalizedBaseUrl}{path}", UriKind.Absolute);
    }

    /// <summary>Builds the failure, carrying the status and — when the API supplied one — the rule
    /// id, so callers can distinguish "rejected this input" from "failed for some other reason".</summary>
    private static async Task<AonikCliException> BuildErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
        {
            return new AonikCliException(
                $"AONIK API call failed with status {status} ({response.ReasonPhrase}).", status, null);
        }

        var sanitizedBody = body.Trim();
        if (sanitizedBody.Length > 400)
        {
            sanitizedBody = sanitizedBody[..400];
        }

        return new AonikCliException(
            $"AONIK API call failed with status {status} ({response.ReasonPhrase}): {sanitizedBody}",
            status,
            TryReadRuleId(body));
    }

    /// <summary>Reads the <c>rule</c> field the API emits for option-validation failures. Best
    /// effort: a body that is not the expected shape simply yields no rule.</summary>
    private static string? TryReadRuleId(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("rule", out var rule)
                && rule.ValueKind == JsonValueKind.String
                    ? rule.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
