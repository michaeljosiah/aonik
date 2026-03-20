namespace Aonik.SharedKernel.Abstractions.Storage;

/// <summary>
/// Result of a file upload operation.
/// </summary>
public record FileUploadResult(
    string StorageProvider,
    string? StorageContainer,
    string StorageKey,
    string ContentType,
    string FileName,
    long FileSizeBytes,
    string Sha256);

/// <summary>
/// Generic, module-agnostic file store abstraction.
/// Uploads, deletes, and generates URLs for files in a content-type-specific
/// blob storage container. Modules inject a named instance configured for
/// their content type (e.g. Attachments, Documents, ProductImages).
/// </summary>
public interface IFileStore
{
    /// <summary>
    /// Uploads a file to blob storage under a tenant/owner path.
    /// </summary>
    Task<FileUploadResult> UploadAsync(
        Guid tenantId,
        Guid ownerEntityId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a blob by its storage key.
    /// </summary>
    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a publicly accessible URL for a given storage key.
    /// For local storage this returns a relative path; for cloud providers
    /// it returns the full CDN/public URL.
    /// </summary>
    string GetUrl(string storageKey);
}
