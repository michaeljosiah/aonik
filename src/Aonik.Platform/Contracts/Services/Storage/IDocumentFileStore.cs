namespace Aonik.Platform.Contracts.Services.Storage;

public record DocumentFileUploadResult(
    string StorageProvider,
    string? StorageContainer,
    string StorageKey,
    string ContentType,
    string FileName,
    long FileSizeBytes,
    string Sha256);

public interface IDocumentFileStore
{
    Task<DocumentFileUploadResult> UploadDocumentFileAsync(
        Guid tenantId,
        Guid documentId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
