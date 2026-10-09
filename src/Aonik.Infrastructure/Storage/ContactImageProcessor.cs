using System.Buffers.Binary;

using ImageMagick;
using ImageMagick.Formats;

using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.Platform.Contracts.Services.ContactEnquiries;

namespace Aonik.Infrastructure.Storage;

/// <summary>Scans originals and converts only JPEG, PNG and HEIC to private, metadata-free JPEGs.</summary>
internal sealed class ContactImageProcessor(ClamAvScanner scanner) : IContactImageProcessor
{
    internal const int MaximumInputBytes = 10 * 1024 * 1024;
    private const int MaximumOutputBytes = 5 * 1024 * 1024;
    private const uint MaximumDimension = 10_000;
    private const ulong MaximumPixels = 40_000_000;
    private static readonly SemaphoreSlim ProcessingSlots = new(2, 2);
    private static readonly Lazy<bool> ResourceLimitsConfigured = new(ConfigureResourceLimits);

    public async Task<ProcessedContactImage> ProcessAsync(ContactImageUpload image, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (image.Content is null || image.Content.Length is 0 or > MaximumInputBytes)
            throw Invalid("image_size", "Choose an image no larger than 10 MB.");
        var format = DetectFormat(image.Content);
        ValidateFileDescription(image, format);
        // Reject excess work instead of retaining a queue of large uploads in the web process.
        if (!await ProcessingSlots.WaitAsync(0, cancellationToken))
            throw Unavailable();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await scanner.ScanAsync(image.Content, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            _ = ResourceLimitsConfigured.Value;
            var settings = new MagickReadSettings
            {
                Format = format,
                FrameIndex = 0,
                FrameCount = 1,
                Defines = format == MagickFormat.Heic ? new HeicReadDefines
                {
                    MaxItems = 128,
                    MaxChildrenPerBox = 128,
                    MaxComponents = 16,
                    MaxIlocExtentsPerItem = 128,
                    MaxNumberOfTiles = 256,
                    MaxSizeEntityGroup = 128,
                    MaxBayerPatternPixels = 40_000_000
                } : null
            };
            using var decoded = new MagickImage();
            // Native ImageMagick also has a processing-time/resource limit. Keep the slot until
            // native work exits; a cancelled request must not leave an unbounded background decode.
            decoded.Progress += (_, progress) => progress.Cancel = timeout.IsCancellationRequested;
            decoded.Ping(image.Content, settings);
            ValidateDimensions(decoded);
            timeout.Token.ThrowIfCancellationRequested();
            decoded.Read(image.Content, settings);
            ValidateDimensions(decoded);
            timeout.Token.ThrowIfCancellationRequested();
            decoded.AutoOrient();
            decoded.Strip();
            if (decoded.Width > 2560 || decoded.Height > 2560)
                decoded.Resize(new MagickGeometry(2560, 2560));
            decoded.BackgroundColor = MagickColors.White;
            decoded.Alpha(AlphaOption.Remove);
            decoded.ColorSpace = ColorSpace.sRGB;
            decoded.Quality = 85;
            using var output = new ContactImageOutputStream(MaximumOutputBytes);
            decoded.Write(output, MagickFormat.Jpeg);
            timeout.Token.ThrowIfCancellationRequested();
            return new ProcessedContactImage(output.ToArray(), "image/jpeg", OutputFileName(image.FileName));
        }
        catch (Exception exception) when (exception is OperationCanceledException || timeout.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw Unavailable();
        }
        catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException or
            TypeInitializationException or MagickMissingDelegateErrorException)
        {
            throw Unavailable();
        }
        catch (MagickResourceLimitErrorException)
        {
            throw Invalid("image_dimensions", "This image is too complex or too large. Choose a smaller image.");
        }
        catch (MagickException)
        {
            throw Invalid("image_invalid", "This image could not be read. Choose a valid JPG, PNG or HEIC image.");
        }
        finally
        {
            ProcessingSlots.Release();
        }
    }

    private static bool ConfigureResourceLimits()
    {
        // Magick is used only by this adapter. Global native limits cover both concurrent slots,
        // forbid pixel-cache spill to disk and keep container/frame/profile allocation bounded.
        ResourceLimits.Memory = 256 * 1024 * 1024;
        ResourceLimits.Disk = 0;
        ResourceLimits.MaxMemoryRequest = 256 * 1024 * 1024;
        ResourceLimits.MaxProfileSize = 1024 * 1024;
        ResourceLimits.Width = MaximumDimension;
        ResourceLimits.Height = MaximumDimension;
        // This native limit also counts intermediate pixel caches during read/resize. The
        // reader still selects exactly one frame; allow its bounded transformation buffers.
        ResourceLimits.ListLength = 8;
        ResourceLimits.Thread = 1;
        ResourceLimits.Time = 20;
        return true;
    }

    private static void ValidateDimensions(MagickImage image)
    {
        if (image.Width == 0 || image.Height == 0 || image.Width > MaximumDimension ||
            image.Height > MaximumDimension || (ulong)image.Width * image.Height > MaximumPixels)
            throw Invalid("image_dimensions", "Choose an image no larger than 40 megapixels or 10,000 pixels on either side.");
    }

    private static MagickFormat DetectFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
            return MagickFormat.Jpeg;
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return MagickFormat.Png;
        if (bytes.Length >= 16 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes);
            if (length >= 16 && length <= 256 && length <= bytes.Length && length % 4 == 0)
            {
                var hasHeicBrand = false;
                for (var offset = 8; offset < length; offset += 4)
                {
                    if (offset == 12) // Minor version is not a compatible brand.
                        continue;
                    var brand = bytes.Slice(offset, 4);
                    if (brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8))
                        throw Invalid("image_format", "Choose a JPG, PNG or HEIC image.");
                    hasHeicBrand |= brand.SequenceEqual("heic"u8) || brand.SequenceEqual("heix"u8) ||
                        brand.SequenceEqual("hevc"u8) || brand.SequenceEqual("hevx"u8);
                }
                if (hasHeicBrand)
                    return MagickFormat.Heic;
            }
        }
        throw Invalid("image_format", "Choose a JPG, PNG or HEIC image.");
    }

    private static void ValidateFileDescription(ContactImageUpload image, MagickFormat format)
    {
        var extension = Path.GetExtension(image.FileName)?.ToLowerInvariant();
        var contentType = image.ContentType?.Trim().ToLowerInvariant();
        var matches = format switch
        {
            MagickFormat.Jpeg => extension is ".jpg" or ".jpeg" && contentType is "image/jpeg" or "" or "application/octet-stream" or null,
            MagickFormat.Png => extension == ".png" && contentType is "image/png" or "" or "application/octet-stream" or null,
            MagickFormat.Heic => extension is ".heic" or ".heif" && contentType is "image/heic" or "image/heif" or "" or "application/octet-stream" or null,
            _ => false
        };
        if (!matches)
            throw Invalid("image_format", "The image format does not match its name or file type. Choose a JPG, PNG or HEIC image.");
    }

    private static string OutputFileName(string name)
    {
        var fileName = Path.GetFileNameWithoutExtension(name.Replace('\\', '/'));
        var safeName = new string(fileName.Where(character => char.IsAsciiLetterOrDigit(character) || character is ' ' or '-' or '_').Take(100).ToArray()).Trim();
        return (safeName.Length == 0 ? "image" : safeName) + ".jpg";
    }

    private static ContactImageValidationException Invalid(string code, string message) => new(code, message);
    private static ContactEnquiryUnavailableException Unavailable() => new("Image processing is temporarily unavailable. Please try again later.");
}
