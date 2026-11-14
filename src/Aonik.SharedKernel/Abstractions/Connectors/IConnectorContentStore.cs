namespace Aonik.SharedKernel.Abstractions.Connectors;

public sealed record ConnectorStoredContent(string FileReference, Guid ConnectionId, string MessageId, string? AttachmentId,
    string FileName, string ContentType, long SizeBytes, string Sha256, DateTime ExpiresAt);

/// <summary>Reuses private file storage. Consumers must reauthorize the referenced connection before opening content.</summary>
public interface IConnectorContentStore
{
    Task<ConnectorStoredContent> SaveAsync(Guid connectionId, string messageId, string? attachmentId, byte[] content,
        string fileName, string contentType, CancellationToken cancellationToken = default);
    ConnectorStoredContent Describe(string fileReference);
    Task<Stream> OpenAsync(string fileReference, CancellationToken cancellationToken = default);
}
