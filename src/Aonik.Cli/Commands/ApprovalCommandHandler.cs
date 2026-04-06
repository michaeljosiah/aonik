using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

public sealed class ApprovalCommandHandler
{
    private readonly IAonikCliApiClient _apiClient;
    private readonly ISessionStore _sessionStore;
    private readonly ICliOutputWriter _outputWriter;

    public ApprovalCommandHandler(
        IAonikCliApiClient apiClient,
        ISessionStore sessionStore,
        ICliOutputWriter outputWriter)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        _outputWriter = outputWriter;
    }

    public async Task<int> ListAsync(OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var proposals = await _apiClient.ListPendingFinancialLifeGraphProposalsAsync(session, cancellationToken);
        await _outputWriter.WriteCollectionAsync(proposals, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ApproveAsync(Guid proposalId, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        await _apiClient.ApproveFinancialLifeGraphProposalAsync(session, proposalId, cancellationToken);
        await _outputWriter.WriteObjectAsync(new
        {
            proposalId,
            status = "approved"
        }, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> RejectAsync(Guid proposalId, string? reason, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        await _apiClient.RejectFinancialLifeGraphProposalAsync(
            session,
            proposalId,
            new RejectFinancialLifeGraphProposalRequest(reason),
            cancellationToken);

        await _outputWriter.WriteObjectAsync(new
        {
            proposalId,
            status = "rejected",
            reason
        }, outputMode, cancellationToken);
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
