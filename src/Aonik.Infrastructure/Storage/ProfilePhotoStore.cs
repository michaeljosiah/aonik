using FluentStorage;
using FluentStorage.Blobs;
using Microsoft.Extensions.Options;
using Aonik.Application.Abstractions.Storage;
using Aonik.Application.Options;

namespace Aonik.Infrastructure.Storage;

/// <summary>
/// Implementation of IProfilePhotoStore using FluentStorage.
/// Handles profile photo uploads, deletions, and URL generation.
/// </summary>
public class ProfilePhotoStore : IProfilePhotoStore
{
    private readonly IBlobStorage _blobStorage;
    private readonly ContentTypeOptions _contentTypeOptions;

    public ProfilePhotoStore(
        IBlobStorage blobStorage,
        IOptions<BlobStorageOptions> storageOptions)
    {
        _blobStorage = blobStorage;
        _contentTypeOptions = storageOptions.Value.ProfilePhotos;
    }

    public async Task<string> UploadCustomerPhotoAsync(
        Guid tenantId,
        Guid partyId,
        string contentType,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only image uploads are supported.", nameof(contentType));
        }

        var blobPath = BuildPhotoBlobPath(tenantId, partyId, contentType);
        await _blobStorage.WriteAsync(blobPath, fileStream, append: false, cancellationToken);

        return GetPhotoUrl(blobPath);
    }

    public async Task DeleteCustomerPhotoAsync(
        string photoUrl,
        CancellationToken cancellationToken = default)
    {
        var blobPath = ExtractBlobPath(photoUrl);
        if (!string.IsNullOrWhiteSpace(blobPath))
        {
            await _blobStorage.DeleteAsync(new[] { blobPath }, cancellationToken);
        }
    }

    public string GetPhotoUrl(string blobPath)
    {
        if (!string.IsNullOrWhiteSpace(_contentTypeOptions.PublicBaseUrl))
        {
            return $"{_contentTypeOptions.PublicBaseUrl.TrimEnd('/')}/{blobPath}";
        }

        return blobPath;
    }

    private string BuildPhotoBlobPath(Guid tenantId, Guid partyId, string contentType)
    {
        var extension = GetFileExtension(contentType);
        var blobName = $"{Guid.NewGuid():N}{extension}";

        return StoragePath.Combine(
            _contentTypeOptions.Path,
            "customers",
            tenantId.ToString("N"),
            partyId.ToString("N"),
            blobName);
    }

    private static string ExtractBlobPath(string photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(photoUrl, UriKind.Absolute, out var uri))
        {
            return uri.AbsolutePath.TrimStart('/');
        }

        return photoUrl;
    }

    private static string GetFileExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            _ => ".jpg"
        };
    }
}
