using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

/// <summary>
/// Drives the <c>documents</c> command group against the Spec 046 Vault
/// linking + filter endpoints (owner-scoped under the document policies).
/// </summary>
public sealed class DocumentCommandHandler
{
    private readonly IAonikCliApiClient _apiClient;
    private readonly ISessionStore _sessionStore;
    private readonly ICliOutputWriter _outputWriter;

    public DocumentCommandHandler(
        IAonikCliApiClient apiClient,
        ISessionStore sessionStore,
        ICliOutputWriter outputWriter)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        _outputWriter = outputWriter;
    }

    public async Task<int> ListAsync(ListDocumentsOptions options, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.ListDocumentsAsync(
            session, options.CareEntityId, options.DocumentType, options.Year, options.Page, options.PageSize, cancellationToken);
        await _outputWriter.WriteObjectAsync(result, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> ListLinksAsync(Guid documentId, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.ListDocumentLinksAsync(session, documentId, cancellationToken);
        await _outputWriter.WriteCollectionAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> LinkAsync(Guid documentId, string targetType, Guid targetId, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetType) || targetId == Guid.Empty)
        {
            throw new AonikCliException("'--target-type' and '--target-id' are required.");
        }

        var session = await RequireSessionAsync(cancellationToken);
        var result = await _apiClient.AddDocumentLinkAsync(session, documentId, new AddDocumentLinkRequest(targetType, targetId), cancellationToken);
        await _outputWriter.WriteObjectAsync(result, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> UnlinkAsync(Guid documentId, Guid linkId, OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        await _apiClient.RemoveDocumentLinkAsync(session, documentId, linkId, cancellationToken);
        await _outputWriter.WriteInfoAsync($"Link {linkId:D} removed from document {documentId:D}.", cancellationToken);
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
