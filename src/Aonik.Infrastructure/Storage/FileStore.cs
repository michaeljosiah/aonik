using System.Security.Cryptography;

using FluentStorage;
using FluentStorage.Blobs;
using Microsoft.Extensions.Options;

using Aonik.SharedKernel.Abstractions.Storage;
using IBlobStorageFactory = Aonik.Application.Abstractions.Storage.IBlobStorageFactory;
using Aonik.Application.Options;

namespace Aonik.Infrastructure.Storage;

/// <summary>
/// Generic, reusable file store implementation backed by FluentStorage.
/// Each instance is parameterised by a <see cref="ContentTypeOptions"/> at
/// construction time, making it suitable for any content category
/// (attachments, documents, product images, etc.).
/// </summary>
public class FileStore : IFileStore
{
    private readonly IBlobStorage _blobStorage;
    private readonly ContentTypeOptions _contentTypeOptions;
    private readonly string _provider;

    public FileStore(
        IBlobStorageFactory blobStorageFactory,
        IOptions<BlobStorageOptions> options,
        ContentTypeOptions contentTypeOptions)
    {
        var storageOptions = options.Value;
        _provider = storageOptions.Provider;
        _contentTypeOptions = contentTypeOptions;
        _blobStorage = blobStorageFactory.Create(_contentTypeOptions);
    }

    public async Task<FileUploadResult> UploadAsync(
        Guid tenantId,
        Guid ownerEntityId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        var resolvedContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();

        var blobPath = BuildBlobPath(tenantId, ownerEntityId, fileName, resolvedContentType);

        string sha256;
        long fileSizeBytes;

        if (fileStream.CanSeek)
        {
            fileSizeBytes = fileStream.Length;
            fileStream.Position = 0;
            sha256 = await ComputeSha256Async(fileStream, cancellationToken);
            fileStream.Position = 0;

            await _blobStorage.WriteAsync(blobPath, fileStream, append: false, cancellationToken);
        }
        else
        {
            await using var buffer = new MemoryStream();
            await fileStream.CopyToAsync(buffer, cancellationToken);
            fileSizeBytes = buffer.Length;
            buffer.Position = 0;
            sha256 = await ComputeSha256Async(buffer, cancellationToken);
            buffer.Position = 0;

            await _blobStorage.WriteAsync(blobPath, buffer, append: false, cancellationToken);
        }

        return new FileUploadResult(
            _provider,
            _contentTypeOptions.ContainerName,
            blobPath,
            resolvedContentType,
            fileName,
            fileSizeBytes,
            sha256);
    }

    public async Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return null;
        }

        return await _blobStorage.OpenReadAsync(storageKey, cancellationToken);
    }

    public async Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return;
        }

        await _blobStorage.DeleteAsync(new[] { storageKey }, cancellationToken);
    }

    public async Task<StagedBlob> StageAsync(
        Guid tenantId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // A temp key per attempt. Two clients staging the same content must not collide here — they
        // converge later, at the content key, which is the only place convergence is safe.
        var tempKey = StoragePath.Combine(
            "workspaces", tenantId.ToString("N"), "staging", $"{Guid.NewGuid():N}");

        await using var hashing = new HashingReadStream(content);

        // Single pass. The hash is computed off the bytes as the store pulls them, so a non-seekable
        // multi-gigabyte upload never has to be buffered to be hashed.
        await _blobStorage.WriteAsync(tempKey, hashing, append: false, cancellationToken);

        return new StagedBlob(tenantId, hashing.GetHashHex(), hashing.BytesRead, tempKey);
    }

    public async Task<PromoteResult> PromoteAsync(
        StagedBlob staged,
        string contentKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(staged);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentKey);

        var existing = await _blobStorage.ExistsAsync([contentKey], cancellationToken);

        if (existing.FirstOrDefault())
        {
            // Someone got here first with byte-identical content — the key IS the hash, so there is
            // nothing to reconcile and nothing worth keeping. Discarding rather than overwriting is
            // what stops concurrent uploads of the same bytes stranding objects nobody references.
            await _blobStorage.DeleteAsync([staged.TempKey], cancellationToken);
            return new PromoteResult(PromoteOutcome.AlreadyPresent, contentKey, staged.SizeBytes);
        }

        await using (var source = await _blobStorage.OpenReadAsync(staged.TempKey, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Staged object {staged.TempKey} is missing; it may have been swept before promotion."))
        {
            await _blobStorage.WriteAsync(contentKey, source, append: false, cancellationToken);
        }

        await _blobStorage.DeleteAsync([staged.TempKey], cancellationToken);

        return new PromoteResult(PromoteOutcome.Stored, contentKey, staged.SizeBytes);
    }

    public string GetUrl(string storageKey)
    {
        if (!string.IsNullOrWhiteSpace(_contentTypeOptions.PublicBaseUrl))
        {
            return $"{_contentTypeOptions.PublicBaseUrl.TrimEnd('/')}/{storageKey}";
        }

        // Local storage — return a relative path served by static file middleware
        return $"/storage/{_contentTypeOptions.Path}/{storageKey}";
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static string BuildBlobPath(
        Guid tenantId,
        Guid ownerEntityId,
        string fileName,
        string contentType)
    {
        var extension = ResolveFileExtension(fileName, contentType);
        var blobName = $"{Guid.NewGuid():N}{extension}";

        return StoragePath.Combine(
            "tenants",
            tenantId.ToString("N"),
            ownerEntityId.ToString("N"),
            blobName);
    }

    private static string ResolveFileExtension(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension;
        }

        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tiff",
            "image/heic" => ".heic",
            "image/svg+xml" => ".svg",
            _ => ".bin"
        };
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
