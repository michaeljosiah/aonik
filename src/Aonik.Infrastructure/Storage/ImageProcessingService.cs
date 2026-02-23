using Aonik.Platform.Contracts.Services.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Aonik.Infrastructure.Storage;

/// <summary>
/// Image processing service using SixLabors.ImageSharp.
/// Handles resizing, thumbnailing, and validation of images.
/// </summary>
public class ImageProcessingService : IImageProcessingService
{
    public async Task ResizeImageAsync(
        Stream sourceStream,
        Stream destinationStream,
        int maxWidth,
        int maxHeight,
        int quality = 85,
        CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync(sourceStream, cancellationToken);

        // Calculate new dimensions while maintaining aspect ratio
        var (newWidth, newHeight) = CalculateResizedDimensions(
            image.Width,
            image.Height,
            maxWidth,
            maxHeight);

        // Resize image
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(newWidth, newHeight),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));

        // Save as JPEG with specified quality
        var encoder = new JpegEncoder { Quality = quality };
        await image.SaveAsync(destinationStream, encoder, cancellationToken);
    }

    public async Task CreateThumbnailAsync(
        Stream sourceStream,
        Stream destinationStream,
        int size = 128,
        int quality = 80,
        CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync(sourceStream, cancellationToken);

        // Create square thumbnail with crop
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(size, size),
            Mode = ResizeMode.Crop,
            Sampler = KnownResamplers.Lanczos3
        }));

        // Save as JPEG with specified quality
        var encoder = new JpegEncoder { Quality = quality };
        await image.SaveAsync(destinationStream, encoder, cancellationToken);
    }

    public async Task<bool> ValidateImageAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await Image.IdentifyAsync(stream, cancellationToken);
            return info != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(int Width, int Height)> GetImageDimensionsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var info = await Image.IdentifyAsync(stream, cancellationToken);
        if (info == null)
        {
            throw new InvalidOperationException("Unable to identify image dimensions.");
        }

        return (info.Width, info.Height);
    }

    private static (int Width, int Height) CalculateResizedDimensions(
        int originalWidth,
        int originalHeight,
        int maxWidth,
        int maxHeight)
    {
        if (originalWidth <= maxWidth && originalHeight <= maxHeight)
        {
            return (originalWidth, originalHeight);
        }

        var ratioX = (double)maxWidth / originalWidth;
        var ratioY = (double)maxHeight / originalHeight;
        var ratio = Math.Min(ratioX, ratioY);

        var newWidth = (int)(originalWidth * ratio);
        var newHeight = (int)(originalHeight * ratio);

        return (newWidth, newHeight);
    }
}
