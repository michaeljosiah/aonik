using FluentStorage;
using FluentStorage.Blobs;
using Microsoft.Extensions.Options;
using Aonik.Application.Abstractions.Storage;
using Aonik.Application.Options;

namespace Aonik.Infrastructure.Storage;

/// <summary>
/// Implementation of IProfilePhotoStore using FluentStorage.
/// Handles profile photo uploads, deletions, and URL generation with thumbnail support.
/// </summary>
public class ProfilePhotoStore : IProfilePhotoStore
{
    private readonly IBlobStorage _blobStorage;
    private readonly ContentTypeOptions _contentTypeOptions;
    private readonly IImageProcessingService _imageProcessingService;

    public ProfilePhotoStore(
        Aonik.Application.Abstractions.Storage.IBlobStorageFactory blobStorageFactory,
        IOptions<BlobStorageOptions> storageOptions,
        IImageProcessingService imageProcessingService)
    {
        _contentTypeOptions = storageOptions.Value.ProfilePhotos;
        _blobStorage = blobStorageFactory.Create(_contentTypeOptions);
        _imageProcessingService = imageProcessingService;
    }

    public async Task<PhotoUploadResult> UploadCustomerPhotoAsync(
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

        // Generate blob paths for original and all thumbnail sizes
        var blobPath = BuildPhotoBlobPath(tenantId, partyId, contentType);
        var mediumThumbPath = BuildThumbnailPath(blobPath, 512);
        var smallThumbPath = BuildThumbnailPath(blobPath, 128);
        var tinyThumbPath = BuildThumbnailPath(blobPath, 64);

        // Save original image (resized to max 1920x1920 for performance)
        using var originalStream = new MemoryStream();
        fileStream.Position = 0;
        await _imageProcessingService.ResizeImageAsync(
            fileStream,
            originalStream,
            maxWidth: 1920,
            maxHeight: 1920,
            quality: 90,
            cancellationToken);

        originalStream.Position = 0;
        await _blobStorage.WriteAsync(blobPath, originalStream, append: false, cancellationToken);

        // Generate medium thumbnail (512x512) for profile pages
        using var mediumThumbStream = new MemoryStream();
        fileStream.Position = 0;
        await _imageProcessingService.CreateThumbnailAsync(
            fileStream,
            mediumThumbStream,
            size: 512,
            quality: 85,
            cancellationToken);

        mediumThumbStream.Position = 0;
        await _blobStorage.WriteAsync(mediumThumbPath, mediumThumbStream, append: false, cancellationToken);

        // Generate small thumbnail (128x128) for avatars
        using var smallThumbStream = new MemoryStream();
        fileStream.Position = 0;
        await _imageProcessingService.CreateThumbnailAsync(
            fileStream,
            smallThumbStream,
            size: 128,
            quality: 80,
            cancellationToken);

        smallThumbStream.Position = 0;
        await _blobStorage.WriteAsync(smallThumbPath, smallThumbStream, append: false, cancellationToken);

        // Generate tiny thumbnail (64x64) for compact lists
        using var tinyThumbStream = new MemoryStream();
        fileStream.Position = 0;
        await _imageProcessingService.CreateThumbnailAsync(
            fileStream,
            tinyThumbStream,
            size: 64,
            quality: 75,
            cancellationToken);

        tinyThumbStream.Position = 0;
        await _blobStorage.WriteAsync(tinyThumbPath, tinyThumbStream, append: false, cancellationToken);

        var originalUrl = GetPhotoUrl(blobPath);
        var mediumThumbUrl = GetPhotoUrl(mediumThumbPath);
        var smallThumbUrl = GetPhotoUrl(smallThumbPath);
        var tinyThumbUrl = GetPhotoUrl(tinyThumbPath);

        return new PhotoUploadResult(originalUrl, mediumThumbUrl, smallThumbUrl, tinyThumbUrl);
    }

    public async Task DeleteCustomerPhotoAsync(
        string photoUrl,
        CancellationToken cancellationToken = default)
    {
        var blobPath = ExtractBlobPath(photoUrl);
        if (!string.IsNullOrWhiteSpace(blobPath))
        {
            // Delete original and all thumbnail sizes
            var mediumThumbPath = BuildThumbnailPath(blobPath, 512);
            var smallThumbPath = BuildThumbnailPath(blobPath, 128);
            var tinyThumbPath = BuildThumbnailPath(blobPath, 64);
            
            await _blobStorage.DeleteAsync(
                new[] { blobPath, mediumThumbPath, smallThumbPath, tinyThumbPath }, 
                cancellationToken);
        }
    }

    public string GetPhotoUrl(string blobPath)
    {
        if (!string.IsNullOrWhiteSpace(_contentTypeOptions.PublicBaseUrl))
        {
            return $"{_contentTypeOptions.PublicBaseUrl.TrimEnd('/')}/{blobPath}";
        }

        // For local storage, return path that will be served by static file middleware
        // Static files are served from /storage/profiles
        return $"/storage/profiles/{blobPath}";
    }

    private string BuildPhotoBlobPath(Guid tenantId, Guid partyId, string contentType)
    {
        var extension = GetFileExtension(contentType);
        var blobName = $"{Guid.NewGuid():N}{extension}";

        return StoragePath.Combine(
            "customers",
            tenantId.ToString("N"),
            partyId.ToString("N"),
            blobName);
    }

    private static string BuildThumbnailPath(string originalPath, int size)
    {
        // Insert "_{size}" before file extension
        // e.g., "customers/abc/def/photo.jpg" -> "customers/abc/def/photo_512.jpg"
        var lastSlash = originalPath.LastIndexOf('/');
        var lastDot = originalPath.LastIndexOf('.');
        
        if (lastDot > lastSlash)
        {
            return originalPath.Insert(lastDot, $"_{size}");
        }
        
        return originalPath + $"_{size}";
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
