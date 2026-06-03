namespace Aonik.Infrastructure.Tests.Documents;

using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Infrastructure.Documents;
using Aonik.SharedKernel.Abstractions.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

/// <summary>
/// Covers the dependency-free text extraction the ingestion pipeline relies on (Spec 035 §13):
/// the plain-text family is read natively, DOCX is parsed from its package with the BCL, and
/// PDF/image content routes to the OCR hook — deferring gracefully when no OCR backend is wired.
/// </summary>
public sealed class DocumentTextExtractorTests
{
    private static DocumentTextExtractor CreateSut(IDocumentOcrExtractor? ocr = null)
        => new(ocr ?? new DeferredDocumentOcrExtractor(), NullLogger<DocumentTextExtractor>.Instance);

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/markdown")]
    [InlineData("application/json")]
    public async Task ExtractTextAsync_Should_Read_PlainText_Family_Natively(string contentType)
    {
        const string content = "the quarterly tax return total was 4200.00";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await CreateSut().ExtractTextAsync(stream, contentType);

        result.Status.Should().Be(ExtractedTextStatus.Native);
        result.HasText.Should().BeTrue();
        result.Text.Should().Be(content);
    }

    [Fact]
    public async Task ExtractTextAsync_Should_Extract_Text_From_Docx()
    {
        using var docx = BuildDocx("Payslip", "Gross pay 5,000.00", "Net pay 3,800.00");

        var result = await CreateSut().ExtractTextAsync(docx, DocxContentType);

        result.Status.Should().Be(ExtractedTextStatus.Native);
        result.Text.Should().Contain("Payslip").And.Contain("Gross pay 5,000.00").And.Contain("Net pay 3,800.00");
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    public async Task ExtractTextAsync_Should_Defer_When_No_Ocr_Backend(string contentType)
    {
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var result = await CreateSut().ExtractTextAsync(stream, contentType);

        result.Status.Should().Be(ExtractedTextStatus.OcrRequired);
        result.HasText.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractTextAsync_Should_Route_To_Ocr_Hook_When_Available()
    {
        var ocr = new Mock<IDocumentOcrExtractor>();
        ocr.SetupGet(o => o.IsAvailable).Returns(true);
        ocr.Setup(o => o.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentTextExtractionResult("scanned passport text", ExtractedTextStatus.OcrDone));
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var result = await CreateSut(ocr.Object).ExtractTextAsync(stream, "image/png");

        result.Status.Should().Be(ExtractedTextStatus.OcrDone);
        result.HasText.Should().BeTrue();
        result.Text.Should().Be("scanned passport text");
    }

    [Fact]
    public async Task ExtractTextAsync_Should_Report_Unsupported_For_Unknown_Type()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await CreateSut().ExtractTextAsync(stream, "application/zip");

        result.Status.Should().Be(ExtractedTextStatus.Unsupported);
        result.HasText.Should().BeFalse();
    }

    [Fact]
    public async Task DeferredOcrExtractor_Should_Report_Unavailable()
    {
        var ocr = new DeferredDocumentOcrExtractor();
        using var stream = new MemoryStream(new byte[] { 1 });

        ocr.IsAvailable.Should().BeFalse();
        (await ocr.ExtractTextAsync(stream, "image/png")).Status.Should().Be(ExtractedTextStatus.OcrRequired);
    }

    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>Builds a minimal valid DOCX (a ZIP with a WordprocessingML <c>word/document.xml</c>).</summary>
    private static Stream BuildDocx(params string[] paragraphs)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            var body = new StringBuilder();
            foreach (var p in paragraphs)
            {
                body.Append("<w:p><w:r><w:t>").Append(p).Append("</w:t></w:r></w:p>");
            }

            writer.Write(
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
                "<w:body>" + body + "</w:body></w:document>");
        }

        stream.Position = 0;
        return stream;
    }
}
