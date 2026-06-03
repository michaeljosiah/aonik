namespace Aonik.SharedKernel.Abstractions.Documents;

/// <summary>
/// Result of persisting a document file's bytes to blob storage.
/// </summary>
public record DocumentFileUploadResult(
    string StorageProvider,
    string? StorageContainer,
    string StorageKey,
    string ContentType,
    string FileName,
    long FileSizeBytes,
    string Sha256);

/// <summary>
/// Blob-storage gateway for document file bytes (Spec 035 §5). The contract lives in SharedKernel
/// so <c>Aonik.Documents</c> (and Compliance) can persist files without a reference to
/// Infrastructure; the implementation (tenant-scoped key, Sha256) stays in <c>Aonik.Infrastructure</c>.
/// </summary>
public interface IDocumentFileStore
{
    Task<DocumentFileUploadResult> UploadDocumentFileAsync(
        Guid tenantId,
        Guid documentId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a previously-uploaded file's bytes for reading by its storage key. Used by the
    /// async ingestion pipeline (Spec 035 §13) to re-load a file for text extraction without
    /// blocking the upload request path. The caller owns disposing the returned stream.
    /// </summary>
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
}
