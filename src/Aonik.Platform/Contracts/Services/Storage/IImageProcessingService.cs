namespace Aonik.Platform.Contracts.Services.Storage;

/// <summary>
/// Service for processing images (resizing, thumbnailing, etc.)
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Resize an image to fit within the specified dimensions while maintaining aspect ratio.
    /// </summary>
    /// <param name="sourceStream">Source image stream</param>
    /// <param name="destinationStream">Destination stream for resized image</param>
    /// <param name="maxWidth">Maximum width in pixels</param>
    /// <param name="maxHeight">Maximum height in pixels</param>
    /// <param name="quality">JPEG quality (1-100), default 85</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ResizeImageAsync(
        Stream sourceStream,
        Stream destinationStream,
        int maxWidth,
        int maxHeight,
        int quality = 85,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a thumbnail version of an image.
    /// </summary>
    /// <param name="sourceStream">Source image stream</param>
    /// <param name="destinationStream">Destination stream for thumbnail</param>
    /// <param name="size">Thumbnail size (width and height)</param>
    /// <param name="quality">JPEG quality (1-100), default 80</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CreateThumbnailAsync(
        Stream sourceStream,
        Stream destinationStream,
        int size = 128,
        int quality = 80,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate that a stream contains a valid image.
    /// </summary>
    /// <param name="stream">Image stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if valid image, false otherwise</returns>
    Task<bool> ValidateImageAsync(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get image dimensions without loading the entire image.
    /// </summary>
    /// <param name="stream">Image stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Width and height tuple</returns>
    Task<(int Width, int Height)> GetImageDimensionsAsync(Stream stream, CancellationToken cancellationToken = default);
}
