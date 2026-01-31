namespace Aonik.Application.Abstractions.Storage;

/// <summary>
/// Service for managing profile photos.
/// Abstracts storage implementation details from application logic.
/// </summary>
public interface IProfilePhotoStore
{
    /// <summary>
    /// Uploads a profile photo for a customer.
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="partyId">Party identifier</param>
    /// <param name="contentType">MIME type of the image</param>
    /// <param name="fileStream">Image data stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Public URL to access the uploaded photo</returns>
    Task<string> UploadCustomerPhotoAsync(
        Guid tenantId,
        Guid partyId,
        string contentType,
        Stream fileStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a profile photo for a customer.
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
