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
            throw new AonikCliException(await BuildErrorAsync(response, cancellationToken));
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
        await SendNoContentAsync(
            session.BaseUrl,
            HttpMethod.Post,
            $"/personal-finance/graph/proposals/{proposalId:D}/approve",
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
        await SendNoContentAsync(
            session.BaseUrl,
            HttpMethod.Post,
            $"/personal-finance/graph/proposals/{proposalId:D}/reject",
            session,
            request,
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
            throw new AonikCliException(await BuildErrorAsync(response, cancellationToken));
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
            throw new AonikCliException(await BuildErrorAsync(response, cancellationToken));
        }
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

    private static async Task<string> BuildErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"AONIK API call failed with status {(int)response.StatusCode} ({response.ReasonPhrase}).";
        }

        var sanitizedBody = body.Trim();
        if (sanitizedBody.Length > 400)
        {
            sanitizedBody = sanitizedBody[..400];
        }

        return $"AONIK API call failed with status {(int)response.StatusCode} ({response.ReasonPhrase}): {sanitizedBody}";
    }
}
