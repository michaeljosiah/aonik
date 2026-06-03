using System.Security.Cryptography;

using FluentStorage;
using FluentStorage.Blobs;
using Microsoft.Extensions.Options;

using Aonik.Application.Abstractions.Storage;
using IBlobStorageFactory = Aonik.Application.Abstractions.Storage.IBlobStorageFactory;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.Application.Options;

namespace Aonik.Infrastructure.Storage;

public class DocumentFileStore : IDocumentFileStore
{
    private readonly IBlobStorage _blobStorage;
    private readonly ContentTypeOptions _contentTypeOptions;
    private readonly string _provider;

    public DocumentFileStore(
        IBlobStorageFactory blobStorageFactory,
        IOptions<BlobStorageOptions> options)
    {
        var storageOptions = options.Value;
        _provider = storageOptions.Provider;
        _contentTypeOptions = storageOptions.Documents;
        _blobStorage = blobStorageFactory.Create(_contentTypeOptions);
    }

    public async Task<DocumentFileUploadResult> UploadDocumentFileAsync(
        Guid tenantId,
        Guid documentId,
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

        var blobPath = BuildDocumentBlobPath(tenantId, documentId, fileName, resolvedContentType);

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

        return new DocumentFileUploadResult(
            _provider,
            _contentTypeOptions.ContainerName,
            blobPath,
            resolvedContentType,
            fileName,
            fileSizeBytes,
            sha256);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("Storage key is required.", nameof(storageKey));
        }

        // The async ingestion pipeline re-loads the uploaded bytes from their tenant-scoped
        // storage key. FluentStorage returns null when the blob is absent (e.g. erased) — surface
        // that as a clear error rather than a NullReferenceException downstream.
        var stream = await _blobStorage.OpenReadAsync(storageKey, cancellationToken);
        return stream ?? throw new InvalidOperationException(
            $"Document blob '{storageKey}' was not found in storage.");
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("Storage key is required.", nameof(storageKey));
        }

        // FluentStorage's batch delete is idempotent — a missing object is a no-op — so the
        // erasure path can re-run safely after a partial failure.
        await _blobStorage.DeleteAsync(new[] { storageKey }, cancellationToken);
    }

    private static string BuildDocumentBlobPath(
        Guid tenantId,
        Guid documentId,
        string fileName,
        string contentType)
    {
        var extension = ResolveFileExtension(fileName, contentType);
        var blobName = $"{Guid.NewGuid():N}{extension}";

        return StoragePath.Combine(
            "tenants",
            tenantId.ToString("N"),
            documentId.ToString("N"),
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
