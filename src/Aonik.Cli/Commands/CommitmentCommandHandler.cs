using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

/// <summary>
/// Drives the <c>commitments</c> lifecycle command group against the Spec 044
/// /personal-finance/commitments endpoints (UserPolicy / PersonalUser).
/// </summary>
public sealed class CommitmentCommandHandler
{
    private readonly IAonikCliApiClient _apiClient;
    private readonly ISessionStore _sessionStore;
    private readonly ICliOutputWriter _outputWriter;

    public CommitmentCommandHandler(
        IAonikCliApiClient apiClient,
        ISessionStore sessionStore,
        ICliOutputWriter outputWriter)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        _outputWriter = outputWriter;
    }

    public async Task<int> CreateAsync(CreateSupportCommitmentOptions options, CancellationToken cancellationToken = default)
    {
        if (options.CareEntityId == Guid.Empty
            || string.IsNullOrWhiteSpace(options.DisplayName)
            || string.IsNullOrWhiteSpace(options.Currency))
        {
            throw new AonikCliException("'--care-entity-id', '--name', and '--currency' are required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.CreateSupportCommitmentAsync(
            session,
            new CreateSupportCommitmentRequest(
                options.CareEntityId,
                options.DisplayName,
                options.ExpectedAmount,
                options.Currency,
                string.IsNullOrWhiteSpace(options.RhythmUnit) ? "Monthly" : options.RhythmUnit,
                options.RhythmInterval <= 0 ? 1 : options.RhythmInterval,
                options.AnchorDay,
                TermDates: null,
                options.FirstDueDate,
                options.ReminderDaysBefore,
                PaidFromAccountId: null,
                options.Notes),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(result, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> MarkDoneAsync(MarkCommitmentDoneOptions options, CancellationToken cancellationToken = default)
    {
        if (options.CommitmentId == Guid.Empty || options.Amount <= 0 || string.IsNullOrWhiteSpace(options.Currency))
        {
            throw new AonikCliException("'<id>', a positive '--amount', and '--currency' are required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.MarkCommitmentDoneAsync(
            session,
            options.CommitmentId,
            new MarkCommitmentDoneRequest(
                options.Amount,
                options.Currency,
                options.ApproxGbp,
                options.Date,
                string.IsNullOrWhiteSpace(options.Channel) ? "bank" : options.Channel,
                options.Note,
                options.IdempotencyKey),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(result, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> SkipAsync(Guid id, string? reason, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.SkipCommitmentAsync(session, id, reason, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> SnoozeAsync(Guid id, DateTime until, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.SnoozeCommitmentAsync(session, id, until, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> PauseAsync(Guid id, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.PauseCommitmentAsync(session, id, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ResumeAsync(Guid id, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.ResumeCommitmentAsync(session, id, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> CyclesAsync(Guid id, int page, int pageSize, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.GetCommitmentCyclesAsync(session, id, page, pageSize, cancellationToken);
        await _outputWriter.WriteCollectionAsync(result, outputMode, cancellationToken);
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
