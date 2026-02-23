namespace Aonik.Platform.Contracts.Services.Storage;

/// <summary>
/// Result of a photo upload operation containing URLs for original and multiple thumbnail sizes.
/// </summary>
/// <param name="OriginalUrl">URL to the original/full-size image (max 1920x1920)</param>
/// <param name="MediumThumbnailUrl">URL to medium thumbnail (512x512) for profile pages</param>
/// <param name="SmallThumbnailUrl">URL to small thumbnail (128x128) for avatars</param>
/// <param name="TinyThumbnailUrl">URL to tiny thumbnail (64x64) for compact lists</param>
public record PhotoUploadResult(
    string OriginalUrl,
    string? MediumThumbnailUrl,
    string? SmallThumbnailUrl,
    string? TinyThumbnailUrl);

/// <summary>
/// Service for managing profile photos.
/// Abstracts storage implementation details from application logic.
/// </summary>
public interface IProfilePhotoStore
{
    /// <summary>
    /// Uploads a profile photo for a customer.
    /// Creates original and multiple thumbnail versions (512x512, 128x128, 64x64).
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="partyId">Party identifier</param>
    /// <param name="contentType">MIME type of the image</param>
    /// <param name="fileStream">Image data stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Photo upload result with URLs for original and all thumbnail sizes</returns>
    Task<PhotoUploadResult> UploadCustomerPhotoAsync(
        Guid tenantId,
        Guid partyId,
        string contentType,
        Stream fileStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a profile photo for a customer (original and all thumbnails).
    /// </summary>
    /// <param name="photoUrl">URL of the photo to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteCustomerPhotoAsync(
        string photoUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the public URL for a photo, or null if not available.
    /// </summary>
    /// <param name="blobPath">Internal blob path</param>
    /// <returns>Public URL or blob path</returns>
    string GetPhotoUrl(string blobPath);
}
