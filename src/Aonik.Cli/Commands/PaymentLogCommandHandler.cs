using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

/// <summary>
/// Drives the <c>payment-logs</c> command group against the Spec 045
/// /personal-finance/payment-logs endpoints (UserPolicy / PersonalUser).
/// </summary>
public sealed class PaymentLogCommandHandler
{
    private readonly IAonikCliApiClient _apiClient;
    private readonly ISessionStore _sessionStore;
    private readonly ICliOutputWriter _outputWriter;

    public PaymentLogCommandHandler(
        IAonikCliApiClient apiClient,
        ISessionStore sessionStore,
        ICliOutputWriter outputWriter)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        _outputWriter = outputWriter;
    }

    public async Task<int> CreateAsync(CreatePaymentLogOptions options, CancellationToken cancellationToken = default)
    {
        if (options.CareEntityId == Guid.Empty || options.Amount <= 0 || string.IsNullOrWhiteSpace(options.Currency))
        {
            throw new AonikCliException("'--care-entity-id', a positive '--amount', and '--currency' are required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.CreatePaymentLogAsync(
            session,
            new CreatePaymentLogRequest(
                options.CareEntityId,
                options.CommitmentId,
                CommitmentCycleId: null,
                options.Amount,
                options.Currency,
                options.ApproxGbp,
                options.Date ?? DateTime.UtcNow.Date,
                string.IsNullOrWhiteSpace(options.Channel) ? "bank" : options.Channel,
                string.IsNullOrWhiteSpace(options.Origin) ? "manual" : options.Origin,
                options.Note,
                options.IdempotencyKey),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(result, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ListAsync(ListPaymentLogsOptions options, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.ListPaymentLogsAsync(
            session, options.CareEntityId, options.CommitmentId, options.Year, options.Page, options.PageSize, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> GetAsync(Guid id, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.GetPaymentLogAsync(session, id, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> UpdateAsync(UpdatePaymentLogOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Id == Guid.Empty || options.Amount <= 0 || string.IsNullOrWhiteSpace(options.Currency))
        {
            throw new AonikCliException("'<id>', a positive '--amount', and '--currency' are required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.UpdatePaymentLogAsync(
            session,
            options.Id,
            new UpdatePaymentLogRequest(
                options.Amount,
                options.Currency,
                options.ApproxGbp,
                options.Date ?? DateTime.UtcNow.Date,
                string.IsNullOrWhiteSpace(options.Channel) ? "bank" : options.Channel,
                options.Note),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(result, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> DeleteAsync(Guid id, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        await _apiClient.DeletePaymentLogAsync(session, id, cancellationToken);
        await _outputWriter.WriteInfoAsync($"Payment log {id:D} soft-deleted (restorable within 30 days).", cancellationToken);
        return 0;
    }

    public async Task<int> RestoreAsync(Guid id, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.RestorePaymentLogAsync(session, id, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> LinkTransactionAsync(Guid id, Guid transactionId, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (transactionId == Guid.Empty)
        {
            throw new AonikCliException("'--transaction-id' is required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.LinkPaymentLogTransactionAsync(session, id, transactionId, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> UnlinkTransactionAsync(Guid id, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.UnlinkPaymentLogTransactionAsync(session, id, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> YearSummaryAsync(int year, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.GetPaymentLogYearSummaryAsync(session, year, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
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
