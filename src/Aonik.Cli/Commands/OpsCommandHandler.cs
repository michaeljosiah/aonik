using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

public sealed class OpsCommandHandler
{
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
