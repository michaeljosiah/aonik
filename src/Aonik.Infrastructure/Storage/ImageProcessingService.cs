using Aonik.SharedKernel.Abstractions.Storage;
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
        bool stripMetadata = false,
        CancellationToken cancellationToken = default)
    {
        using var image = await LoadImageAsync(sourceStream, cancellationToken);

        if (stripMetadata)
        {
            // Apply camera orientation before removing the profile that records it.
            image.Mutate(x => x.AutoOrient());
            image.Metadata.ExifProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.CicpProfile = null;
        }

        if (image.Width > maxWidth || image.Height > maxHeight)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(maxWidth, maxHeight),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));
        }

        // Save as JPEG with specified quality
        var encoder = new JpegEncoder { Quality = quality, SkipMetadata = stripMetadata };
        await image.SaveAsync(destinationStream, encoder, cancellationToken);
    }

    public async Task CreateThumbnailAsync(
        Stream sourceStream,
        Stream destinationStream,
        int size = 128,
        int quality = 80,
        CancellationToken cancellationToken = default)
    {
        using var image = await LoadImageAsync(sourceStream, cancellationToken);

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

    public async Task<string?> DetectContentTypeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            return (await Image.DetectFormatAsync(stream, cancellationToken)).DefaultMimeType;
        }
        catch (UnknownImageFormatException)
        {
            return null;
        }
    }

    public async Task<(int Width, int Height)> GetImageDimensionsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await Image.IdentifyAsync(stream, cancellationToken);
            return (info.Width, info.Height);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new ArgumentException("Choose a valid JPEG or PNG image.", nameof(stream), ex);
        }
    }

    private static async Task<Image> LoadImageAsync(Stream sourceStream, CancellationToken cancellationToken)
    {
        try
        {
            // Every output here is a single JPEG; do not allocate discarded animation frames.
            return await Image.LoadAsync(new DecoderOptions { MaxFrames = 1 }, sourceStream, cancellationToken);
        }
        catch (UnknownImageFormatException ex)
        {
            throw new ArgumentException(
                "The selected photo format is not supported. Please choose a JPG or PNG image.",
                nameof(sourceStream),
                ex);
        }
        catch (InvalidImageContentException ex)
        {
            throw new ArgumentException(
                "The selected photo could not be processed. Please choose a valid JPG or PNG image.",
                nameof(sourceStream),
                ex);
        }
    }
}
