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
    /// Opens a read stream for a blob by its storage key. Returns <see langword="null"/> if no blob
    /// exists at the key. The caller owns the returned stream and must dispose it.
    /// </summary>
    Task<Stream?> OpenReadAsync(
        string storageKey,
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

    /// <summary>
    /// Streams content to a temporary key, hashing as it goes (Spec 089 §5).
    ///
    /// <para>
    /// <see cref="UploadAsync"/> cannot serve content addressing, and saying otherwise hid a real prerequisite.
    /// It returns a SHA-256 — but <em>after</em> writing the object to a randomly-named GUID path it chose
    /// itself. The hash is an <strong>output, never an input</strong>, so a key derived from the hash cannot be
    /// produced by it. Two identical uploads would write two physical objects and only the database row would
    /// dedupe, leaving the second stranded and paid for.
    /// </para>
    ///
    /// <para>
    /// Nothing is materialised in memory: the hash is computed off the bytes as they stream past.
    /// </para>
    /// </summary>
    Task<StagedBlob> StageAsync(
        Guid tenantId,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a staged object to its content key (Spec 089 §5).
    ///
    /// <para>
    /// <strong>If the key already exists the staged copy is discarded</strong> and the result reports
    /// <see cref="PromoteOutcome.AlreadyPresent"/>. That is the whole concurrency answer: last writer discards
    /// rather than duplicates. Because the key <em>is</em> the hash, a racing writer that gets there first wrote
    /// byte-identical content, so there is nothing to reconcile.
    /// </para>
    /// </summary>
    Task<PromoteResult> PromoteAsync(
        StagedBlob staged,
        string contentKey,
        CancellationToken cancellationToken = default);
}

/// <param name="ContentHash">Lowercase hex SHA-256, computed while streaming.</param>
/// <param name="TempKey">Where the bytes are until they are promoted or swept.</param>
public sealed record StagedBlob(
    Guid TenantId,
    string ContentHash,
    long SizeBytes,
    string TempKey);

public enum PromoteOutcome
{
    /// <summary>The staged object became the content object.</summary>
    Stored = 0,

    /// <summary>Something already held these exact bytes; the staged copy was discarded.</summary>
    AlreadyPresent = 1,
}

public sealed record PromoteResult(PromoteOutcome Outcome, string ContentKey, long SizeBytes);
