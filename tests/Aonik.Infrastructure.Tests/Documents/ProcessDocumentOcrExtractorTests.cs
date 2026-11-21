using System.Text;

using FluentAssertions;
using Microsoft.Extensions.Options;

using Aonik.Infrastructure.Documents;
using Aonik.Infrastructure.Storage;
using Aonik.Infrastructure.Tests.Storage;
using Aonik.SharedKernel.Abstractions.Documents;

namespace Aonik.Infrastructure.Tests.Documents;

public class ProcessDocumentOcrExtractorTests
{
    [SkippableTheory]
    [InlineData("sales-invoice.png", "image/png", "Tesseract", "250.00")]
    [InlineData("sales-invoice-native.pdf", "application/pdf", "Poppler", "250.00")]
    [InlineData("supplier-invoice-scanned.pdf", "application/pdf", "Tesseract", "80.00")]
    public async Task ConfiguredNativeEngines_Should_ExtractRealSyntheticDocuments_WithRecordedProvenance(string fileName, string contentType, string engine, string total)
    {
        var settings = new DocumentOcrOptions { Enabled = true };
        await using var scanner = new ScriptedClamAvServer();
        var extractor = new ProcessDocumentOcrExtractor(Options.Create(settings), scanner.Scanner);
        bool available = extractor.IsAvailable && File.Exists(settings.PdfToTextPath) && File.Exists(settings.PdfToPpmPath);
        if (Environment.GetEnvironmentVariable("AONIK_OCR_TESTS_REQUIRED") == "true") available.Should().BeTrue("the CI OCR lane must run real engines rather than silently skip");
        Skip.IfNot(available, "Configure Tesseract and Poppler to run the real OCR qualification.");
        await using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "DocumentFixtures", fileName));

        DocumentTextExtractionResult result = await extractor.ExtractTextAsync(input, contentType);

        result.HasText.Should().BeTrue(); result.Text.Should().Contain("INVOICE").And.Contain(total).And.Contain("GBP");
        result.Provenance.Should().NotBeNull(); result.Provenance!.Extractor.Should().Be(engine); result.Provenance.Pages.Should().Be(1);
        result.Provenance.Version.Should().NotBe("unknown");
        if (engine == "Tesseract") { result.Status.Should().Be(ExtractedTextStatus.OcrDone); result.Provenance.Confidence.Should().BeGreaterThan(0.7); }
        else result.Status.Should().Be(ExtractedTextStatus.Native);
    }

    [Fact]
    public async Task DisabledBackend_Should_DeferWithoutReadingUntrustedContent()
    {
        var extractor = new ProcessDocumentOcrExtractor(Options.Create(new DocumentOcrOptions()), DisabledScanner());
        extractor.IsAvailable.Should().BeFalse();
        (await extractor.ExtractTextAsync(Stream.Null, "application/pdf")).Status.Should().Be(ExtractedTextStatus.OcrRequired);
    }

    [Fact]
    public async Task MalformedOrOversizedSources_Should_BeRejectedBeforeStartingTheConfiguredProcess()
    {
        var extractor = LocalValidation();
        using var malformed = new MemoryStream("not-a-png"u8.ToArray());
        await ((Func<Task>)(() => extractor.ExtractTextAsync(malformed, "image/png"))).Should().ThrowAsync<DocumentTextExtractionException>().Where(error => error.Code == "DocumentSignatureMismatch");
        using var oversized = new MemoryStream(new byte[10 * 1024 * 1024 + 1]);
        await ((Func<Task>)(() => extractor.ExtractTextAsync(oversized, "image/png"))).Should().ThrowAsync<DocumentTextExtractionException>().Where(error => error.Code == "DocumentSizeLimit");
    }

    [Fact]
    public async Task LanguageConfiguration_Should_NotBecomeCommandLineArguments()
    {
        var extractor = new ProcessDocumentOcrExtractor(Options.Create(new DocumentOcrOptions { Enabled = true, TesseractPath = Environment.ProcessPath!, Language = "eng --user-words /private" }), DisabledScanner());
        await ((Func<Task>)(() => extractor.ExtractTextAsync(Stream.Null, "image/png"))).Should().ThrowAsync<DocumentTextExtractionException>().Where(error => error.Code == "OcrConfigurationInvalid");
    }

    [Fact]
    public void Tsv_Should_PreserveLinesAndBoundedConfidenceWithoutTreatingItAsApproval()
    {
        string tsv = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n"
            + "5\t1\t1\t1\t1\t1\t0\t0\t100\t20\t90\tInvoice\n5\t1\t1\t1\t2\t1\t0\t20\t100\t20\t70\t250.00\n";
        (string text, double? confidence) = ProcessDocumentOcrExtractor.ReadTsv(tsv);
        text.Should().Be("Invoice\n250.00"); confidence.Should().BeApproximately(0.8, 0.00001);
        ((Action)(() => ProcessDocumentOcrExtractor.ReadTsv(tsv.Replace("90\t", "NaN\t", StringComparison.Ordinal)))).Should().Throw<DocumentTextExtractionException>();
    }

    [Fact]
    public async Task UnscannedOrUnsafeSources_Should_NeverReachNativeParsing()
    {
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "DocumentFixtures", "sales-invoice.png"));
        using var unscanned = new MemoryStream(bytes);
        await ((Func<Task>)(() => LocalValidation().ExtractTextAsync(unscanned, "image/png"))).Should().ThrowAsync<DocumentTextExtractionException>().Where(error => error.Code == "DocumentSafetyUnavailable");
        await using var scanner = new ScriptedClamAvServer("stream: Test-Malware FOUND\0");
        var extractor = new ProcessDocumentOcrExtractor(Options.Create(new DocumentOcrOptions { Enabled = true, TesseractPath = Environment.ProcessPath! }), scanner.Scanner);
        using var unsafeInput = new MemoryStream(bytes);
        await ((Func<Task>)(() => extractor.ExtractTextAsync(unsafeInput, "image/png"))).Should().ThrowAsync<DocumentTextExtractionException>().Where(error => error.Code == "DocumentUnsafe");
    }

    private static ClamAvScanner DisabledScanner() => new(Options.Create(new ClamAvOptions()));
    private static ProcessDocumentOcrExtractor LocalValidation() => new(Options.Create(new DocumentOcrOptions { Enabled = true, TesseractPath = Environment.ProcessPath! }), DisabledScanner());
}
