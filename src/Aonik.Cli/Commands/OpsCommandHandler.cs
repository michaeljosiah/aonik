using System.Text.Json;
using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

public sealed class OpsCommandHandler
{
    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAonikCliApiClient _apiClient;
    private readonly ISessionStore _sessionStore;
    private readonly ICliOutputWriter _outputWriter;

    public OpsCommandHandler(
        IAonikCliApiClient apiClient,
        ISessionStore sessionStore,
        ICliOutputWriter outputWriter)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        _outputWriter = outputWriter;
    }

    public async Task<int> RunWorkflowAsync(RunWorkflowOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.WorkflowName) || string.IsNullOrWhiteSpace(options.Input))
        {
            throw new AonikCliException("'--workflow-name' and '--input' are required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.RunWorkflowAsync(
            session,
            new WorkflowRequest(options.WorkflowName, options.Input),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ListJobsAsync(OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.ListScheduledJobsAsync(session, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> SchedulerHealthAsync(OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.GetSchedulerHealthAsync(session, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> TriggerJobAsync(JobTriggerOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.JobName))
        {
            throw new AonikCliException("'--job-name' is required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.TriggerScheduledJobAsync(session, options.JobName, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ListLedgersAsync(OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var ledgers = await _apiClient.ListLedgersAsync(session, cancellationToken);
        await _outputWriter.WriteCollectionAsync(ledgers, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> CreateLedgerAsync(CreateLedgerOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.BaseCurrency))
        {
            throw new AonikCliException("'--base-currency' is required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.CreateLedgerAsync(
            session,
            new CreateLedgerRequest(options.BaseCurrency),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ListInvoicesAsync(ListInvoicesOptions options, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var invoices = await _apiClient.ListInvoicesAsync(session, options.Status, cancellationToken);
        await _outputWriter.WriteCollectionAsync(invoices, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> CreatePaymentIntentAsync(CreatePaymentIntentOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Currency) || string.IsNullOrWhiteSpace(options.Reference) || options.OrderId == Guid.Empty)
        {
            throw new AonikCliException("'--currency', '--reference', and '--order-id' are required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.CreatePaymentIntentAsync(
            session,
            new CreatePaymentIntentRequest(options.Amount, options.Currency, options.Reference, options.OrderId, options.InvoiceId),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> GetPaymentIntentAsync(Guid paymentIntentId, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.GetPaymentIntentAsync(session, paymentIntentId, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> CapturePaymentAsync(Guid paymentIntentId, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.CapturePaymentAsync(session, paymentIntentId, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> CancelPaymentAsync(Guid paymentIntentId, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.CancelPaymentAsync(session, paymentIntentId, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> GetInvoiceAsync(Guid invoiceId, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.GetInvoiceAsync(session, invoiceId, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> CreateInvoiceAsync(CreateInvoiceOptions options, CancellationToken cancellationToken = default)
    {
        if (options.CustomerId == Guid.Empty
            || string.IsNullOrWhiteSpace(options.InvoiceNumber)
            || string.IsNullOrWhiteSpace(options.Currency))
        {
            throw new AonikCliException("'--customer-id', '--invoice-number', and '--currency' are required.");
        }

        var lineItems = await ReadJsonListAsync<CreateInvoiceLineItemInput>(options.LinesFile, cancellationToken)
            ?? new List<CreateInvoiceLineItemInput>();

        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.CreateInvoiceAsync(
            session,
            new CreateInvoiceRequest(
                options.CustomerId,
                options.InvoiceNumber,
                options.Currency,
                options.DueUtc,
                lineItems),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> IssueInvoiceAsync(InvoiceMutationOptions options, CancellationToken cancellationToken = default)
    {
        EnsureConfirmed(options.Confirm, "issue invoice");
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.IssueInvoiceAsync(session, options.InvoiceId, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> CancelInvoiceAsync(InvoiceMutationOptions options, CancellationToken cancellationToken = default)
    {
        EnsureConfirmed(options.Confirm, "cancel invoice");
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.CancelInvoiceAsync(session, options.InvoiceId, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> MarkInvoicePaidAsync(InvoiceMutationOptions options, CancellationToken cancellationToken = default)
    {
        EnsureConfirmed(options.Confirm, "mark invoice paid");
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.MarkInvoicePaidAsync(session, options.InvoiceId, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ListOrdersAsync(ListOrdersOptions options, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.ListOrdersAsync(
            session,
            new ListOrdersRequest(
                options.Page,
                options.PageSize,
                options.Status,
                options.OrderType,
                options.Search,
                options.PayerPartyId),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> GetOrderAsync(Guid orderId, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.GetOrderAsync(session, orderId, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> CreateBillPaymentOrderAsync(CreateBillPaymentOrderOptions options, CancellationToken cancellationToken = default)
    {
        if (options.PayerPartyId == Guid.Empty
            || string.IsNullOrWhiteSpace(options.OriginCountry)
            || string.IsNullOrWhiteSpace(options.OriginCurrency))
        {
            throw new AonikCliException("'--payer-party-id', '--origin-country', and '--origin-currency' are required.");
        }

        var items = await ReadJsonListAsync<CreateBillPaymentItemInput>(options.ItemsFile, cancellationToken);

        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.CreateBillPaymentOrderAsync(
            session,
            new CreateBillPaymentOrderRequest(
                options.PayerPartyId,
                options.OriginCountry,
                options.OriginCurrency,
                options.PurposeCode,
                options.Notes,
                items),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> SubmitOrderAsync(SubmitOrderOptions options, CancellationToken cancellationToken = default)
    {
        EnsureConfirmed(options.Confirm, "submit order");
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.SubmitOrderAsync(session, options.OrderId, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> CancelOrderAsync(CancelOrderOptions options, CancellationToken cancellationToken = default)
    {
        EnsureConfirmed(options.Confirm, "cancel order");
        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.CancelOrderAsync(session, options.OrderId, options.Reason, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> GetScheduledJobAsync(string jobName, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
        {
            throw new AonikCliException("'--job-name' is required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.GetScheduledJobDetailAsync(session, jobName, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> PauseScheduledJobAsync(string jobName, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
        {
            throw new AonikCliException("'--job-name' is required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.PauseScheduledJobAsync(session, jobName, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ResumeScheduledJobAsync(string jobName, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
        {
            throw new AonikCliException("'--job-name' is required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.ResumeScheduledJobAsync(session, jobName, cancellationToken);
        await _outputWriter.WriteObjectAsync(response, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ListScheduledJobRunsAsync(ListJobRunsOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.JobName))
        {
            throw new AonikCliException("'--job-name' is required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var response = await _apiClient.ListScheduledJobRunsAsync(
            session,
            options.JobName,
            options.Page,
            options.PageSize,
            cancellationToken);

        await _outputWriter.WriteObjectAsync(response, options.OutputMode, cancellationToken);
        return 0;
    }

    private static void EnsureConfirmed(bool confirm, string action)
    {
        if (!confirm)
        {
            throw new AonikCliException(
                $"Refusing to {action} without '--confirm'. This is a financially material operation — re-run with --confirm to proceed.");
        }
    }

    private static async Task<List<T>?> ReadJsonListAsync<T>(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            throw new AonikCliException($"File not found: {path}");
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonReadOptions);
        }
        catch (JsonException ex)
        {
            throw new AonikCliException($"Failed to parse JSON file '{path}': {ex.Message}");
        }
    }

    private async Task<CliSession> RequireSessionAsync(CancellationToken cancellationToken)
    {
        var session = await _sessionStore.LoadAsync(cancellationToken);
        if (session is null)
        {
            throw new AonikCliException("No active session found. Run 'aonik auth login' first.");
        }

        return session;
    }
}
