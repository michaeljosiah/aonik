using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

/// <summary>
/// Drives the <c>circle</c> command group against the Spec 048 sharing +
/// Support Statement endpoints (UserPolicy / PersonalUser).
/// </summary>
public sealed class CircleCommandHandler
{
    private readonly IAonikCliApiClient _apiClient;
    private readonly ISessionStore _sessionStore;
    private readonly ICliOutputWriter _outputWriter;

    public CircleCommandHandler(
        IAonikCliApiClient apiClient,
        ISessionStore sessionStore,
        ICliOutputWriter outputWriter)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        _outputWriter = outputWriter;
    }

    public async Task<int> GrantAsync(CreateCircleGrantOptions options, CancellationToken cancellationToken = default)
    {
        if (options.MemberUserId == Guid.Empty || string.IsNullOrWhiteSpace(options.Scope))
        {
            throw new AonikCliException("'--member-user-id' and '--scope' are required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.CreateCircleGrantAsync(
            session,
            new CreateCircleGrantRequest(options.MemberUserId, options.Scope, options.EntityIds, options.NoAmounts),
            cancellationToken);
        await _outputWriter.WriteObjectAsync(result, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ListGrantsAsync(OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.ListCircleGrantsAsync(session, cancellationToken);
        await _outputWriter.WriteCollectionAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ListSharedAsync(OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.ListCircleSharedWithMeAsync(session, cancellationToken);
        await _outputWriter.WriteCollectionAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> RevokeAsync(Guid grantId, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        await _apiClient.RevokeCircleGrantAsync(session, grantId, cancellationToken);
        await _outputWriter.WriteInfoAsync($"Grant {grantId:D} revoked.", cancellationToken);
        return 0;
    }

    public async Task<int> InviteAsync(CreateCircleInviteOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Scope))
        {
            throw new AonikCliException("'--scope' is required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.CreateCircleInviteAsync(
            session,
            new CreateCircleInviteRequest(options.Scope, options.EntityIds, options.NoAmounts, options.Channel),
            cancellationToken);
        await _outputWriter.WriteObjectAsync(result, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> AcceptAsync(string token, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new AonikCliException("'--token' is required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.AcceptCircleInviteAsync(session, token, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> StatementAsync(Guid careEntityId, DateTime? from, DateTime? to, string? preparedFor, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.GetSupportStatementAsync(session, careEntityId, from, to, preparedFor, cancellationToken);
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
