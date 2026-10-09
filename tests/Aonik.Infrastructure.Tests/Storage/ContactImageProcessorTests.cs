using System.Buffers.Binary;

using FluentAssertions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

using Aonik.Infrastructure.Storage;
using Aonik.Platform.Contracts.Models.ContactEnquiries;

namespace Aonik.Infrastructure.Tests.Storage;

public class ContactImageProcessorTests
{
    [Fact]
    public async Task Process_Should_ScanOriginalThenOrientJpeg_AndRemovePrivateMetadata()
    {
        var original = await JpegAsync();
        await using var server = new ScriptedClamAvServer();
        var processor = new ContactImageProcessor(server.Scanner);

        var result = await processor.ProcessAsync(new ContactImageUpload("../../private\\photo.jpg", "image/jpeg", original));

        (await server.Received.Task).Should().Equal(original);
        result.FileName.Should().Be("photo.jpg");
        result.ContentType.Should().Be("image/jpeg");
        using var output = Image.Load(result.Content);
        output.Width.Should().Be(10);
        output.Height.Should().Be(20);
        output.Metadata.ExifProfile.Should().BeNull();
        output.Metadata.XmpProfile.Should().BeNull();
        output.Metadata.IccProfile.Should().BeNull();
        output.Metadata.IptcProfile.Should().BeNull();
    }

    [Fact]
    public async Task Process_Should_ConvertPngToBoundedJpeg_WithWhiteTransparency()
    {
        using var source = new Image<Rgba32>(3000, 100);
        using var stream = new MemoryStream();
        await source.SaveAsPngAsync(stream);
        await using var server = new ScriptedClamAvServer();

        var result = await new ContactImageProcessor(server.Scanner).ProcessAsync(
            new ContactImageUpload("image.png", "image/png", stream.ToArray()));

        result.ContentType.Should().Be("image/jpeg");
        result.FileName.Should().Be("image.jpg");
        using var output = Image.Load<Rgb24>(result.Content);
        output.Width.Should().Be(2560);
        output.Height.Should().BeInRange(85, 86);
        output[0, 0].R.Should().BeGreaterThan(250);
        output[0, 0].G.Should().BeGreaterThan(250);
        output[0, 0].B.Should().BeGreaterThan(250);
        result.Content.Length.Should().BeLessThan(5 * 1024 * 1024);
    }

    [Theory]
    [InlineData("image/heic")]
    [InlineData("")]
    public async Task Process_Should_DecodeRealHeicFixture_AsJpeg(string contentType)
    {
        var original = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Storage", "Fixtures", "rainbow.heic"));
        await using var server = new ScriptedClamAvServer();

        var result = await new ContactImageProcessor(server.Scanner).ProcessAsync(
            new ContactImageUpload("rainbow.heic", contentType, original));

        (await server.Received.Task).Should().Equal(original);
        result.ContentType.Should().Be("image/jpeg");
        result.FileName.Should().Be("rainbow.jpg");
        using var output = Image.Load(result.Content);
        output.Width.Should().Be(451);
        output.Height.Should().Be(461);
        output.Metadata.ExifProfile.Should().BeNull();
    }

    [Fact]
    public async Task Process_Should_ReportVirusBeforeAttemptingToDecodeCorruptPixels()
    {
        await using var server = new ScriptedClamAvServer("stream: Test.Signature FOUND\0");
        var action = () => new ContactImageProcessor(server.Scanner).ProcessAsync(
            new ContactImageUpload("bad.jpg", "image/jpeg", new byte[] { 255, 216, 255, 0 }));

        var error = (await action.Should().ThrowAsync<ContactImageValidationException>()).Which;

        error.Code.Should().Be("image_unsafe");
    }

    [Fact]
    public async Task Process_Should_RejectCorruptImageAfterCleanVerdict()
    {
        await using var server = new ScriptedClamAvServer();
        var action = () => new ContactImageProcessor(server.Scanner).ProcessAsync(
            new ContactImageUpload("bad.jpg", "image/jpeg", new byte[] { 255, 216, 255, 0 }));

        var error = (await action.Should().ThrowAsync<ContactImageValidationException>()).Which;

        error.Code.Should().Be("image_invalid");
    }

    [Theory]
    [InlineData("wrong.png", "image/jpeg")]
    [InlineData("wrong.jpg", "image/png")]
    [InlineData("wrong.svg", "image/svg+xml")]
    public async Task Process_Should_RejectSpoofedFileDescription_BeforeScanner(string fileName, string contentType)
    {
        var processor = UnconfiguredProcessor();
        var original = await JpegAsync();

        var action = () => processor.ProcessAsync(new ContactImageUpload(fileName, contentType, original));

        var error = (await action.Should().ThrowAsync<ContactImageValidationException>()).Which;
        error.Code.Should().Be("image_format");
    }

    [Theory]
    [InlineData("image.svg", "image/svg+xml", "<svg xmlns='http://www.w3.org/2000/svg'/>")]
    [InlineData("image.jpg", "image/jpeg", "%PDF-1.7")]
    [InlineData("image.heic", "image/heic", "not a HEIC container")]
    public async Task Process_Should_RejectOtherFormatsWithoutInvokingNativeDecoderOrScanner(string fileName, string contentType, string content)
    {
        var action = () => UnconfiguredProcessor().ProcessAsync(
            new ContactImageUpload(fileName, contentType, System.Text.Encoding.UTF8.GetBytes(content)));

        var error = (await action.Should().ThrowAsync<ContactImageValidationException>()).Which;

        error.Code.Should().Be("image_format");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10 * 1024 * 1024 + 1)]
    public async Task Process_Should_RejectInvalidByteSize_BeforeScanner(int size)
    {
        var action = () => UnconfiguredProcessor().ProcessAsync(new ContactImageUpload("image.jpg", "image/jpeg", new byte[size]));

        var error = (await action.Should().ThrowAsync<ContactImageValidationException>()).Which;

        error.Code.Should().Be("image_size");
    }

    [Fact]
    public async Task Process_Should_FailClosed_WhenScannerIsNotConfigured()
    {
        var image = new ContactImageUpload("image.jpg", "image/jpeg", await JpegAsync());

        var action = () => UnconfiguredProcessor().ProcessAsync(image);

        await action.Should().ThrowAsync<ContactEnquiryUnavailableException>();
    }

    [Theory]
    [InlineData(10_001u, 1u)]
    [InlineData(7000u, 6000u)]
    public async Task Process_Should_RejectExcessiveDimensionsBeforePixelAllocation(uint width, uint height)
    {
        using var tiny = new Image<Rgba32>(1, 1);
        using var source = new MemoryStream();
        await tiny.SaveAsPngAsync(source);
        var bytes = source.ToArray();
        // A valid PNG IHDR declaring oversized dimensions, without allocating those pixels.
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20, 4), height);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(29, 4), Crc32(bytes.AsSpan(12, 17)));
        await using var server = new ScriptedClamAvServer();

        var action = () => new ContactImageProcessor(server.Scanner).ProcessAsync(new ContactImageUpload("large.png", "image/png", bytes));

        await action.Should().ThrowAsync<ContactImageValidationException>();
    }

    [Fact]
    public async Task Process_Should_PreserveCallerCancellation_AndReleaseProcessingSlot()
    {
        var image = new ContactImageUpload("image.jpg", "image/jpeg", await JpegAsync());
        await using var server = new ScriptedClamAvServer(holdResponse: true);
        using var cancellation = new CancellationTokenSource();
        var pending = new ContactImageProcessor(server.Scanner).ProcessAsync(image, cancellation.Token);
        await server.Received.Task;

        await cancellation.CancelAsync();

        await ((Func<Task>)(async () => await pending)).Should().ThrowAsync<OperationCanceledException>();
        await using var nextServer = new ScriptedClamAvServer();
        var result = await new ContactImageProcessor(nextServer.Scanner).ProcessAsync(image);
        result.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Process_Should_RejectExcessConcurrentWork_InsteadOfQueuingUploads()
    {
        var image = new ContactImageUpload("image.jpg", "image/jpeg", await JpegAsync());
        await using var firstServer = new ScriptedClamAvServer(holdResponse: true);
        await using var secondServer = new ScriptedClamAvServer(holdResponse: true);
        using var cancellation = new CancellationTokenSource();
        var first = new ContactImageProcessor(firstServer.Scanner).ProcessAsync(image, cancellation.Token);
        var second = new ContactImageProcessor(secondServer.Scanner).ProcessAsync(image, cancellation.Token);
        await Task.WhenAll(firstServer.Received.Task, secondServer.Received.Task);

        var third = () => UnconfiguredProcessor().ProcessAsync(image);

        await third.Should().ThrowAsync<ContactEnquiryUnavailableException>();
        await cancellation.CancelAsync();
        await ((Func<Task>)(() => Task.WhenAll(first, second))).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Output_Should_StopBeforeExceedingByteLimit()
    {
        using var output = new ContactImageOutputStream(4);
        output.Write(new byte[] { 1, 2, 3 });

        var action = () => output.Write(new byte[] { 4, 5 });

        action.Should().Throw<ContactImageValidationException>();
        output.Length.Should().Be(3);
    }

    private static ContactImageProcessor UnconfiguredProcessor() => new(new ClamAvScanner(Options.Create(new ClamAvOptions())));

    private static async Task<byte[]> JpegAsync()
    {
        using var image = new Image<Rgba32>(20, 10);
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
        image.Metadata.ExifProfile.SetValue(ExifTag.Artist, "Private sender name");
        image.Metadata.ExifProfile.SetValue(ExifTag.GPSLatitudeRef, "N");
        using var source = new MemoryStream();
        await image.SaveAsJpegAsync(source);
        return source.ToArray();
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
        }
        return ~crc;
    }
}
