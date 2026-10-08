using Aonik.Infrastructure.Storage;

using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Aonik.Infrastructure.Tests.Storage;

public class ImageProcessingServiceTests
{
    [Fact]
    public async Task ResizePublicImage_Should_ApplyOrientation_AndStripEmbeddedMetadata()
    {
        using var source = new MemoryStream();
        using (var image = new Image<Rgba32>(20, 10))
        {
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
            image.Metadata.ExifProfile.SetValue(ExifTag.Artist, "Private embedded metadata");
            await image.SaveAsJpegAsync(source);
        }
        source.Position = 0;
        using var output = new MemoryStream();

        await new ImageProcessingService().ResizeImageAsync(source, output, 1920, 1920, stripMetadata: true);

        output.Position = 0;
        using var decoded = await Image.LoadAsync(output);
        decoded.Width.Should().Be(10);
        decoded.Height.Should().Be(20);
        decoded.Metadata.ExifProfile.Should().BeNull();
        decoded.Metadata.IccProfile.Should().BeNull();
    }

    [Fact]
    public async Task DetectContentType_Should_RecognizeUnsupportedContainer_WithoutDecodingIt()
    {
        // A TIFF header with no image directory: format detection need not parse the container.
        using var source = new MemoryStream(new byte[] { 0x49, 0x49, 0x2a, 0, 8, 0, 0, 0, 0, 0 });

        var result = await new ImageProcessingService().DetectContentTypeAsync(source);

        result.Should().Be("image/tiff");
    }
}
