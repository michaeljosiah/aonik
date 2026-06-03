using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

using Aonik.SharedKernel.Abstractions.Documents;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Documents;

/// <summary>
/// Default <see cref="IDocumentTextExtractor"/> for the RAG ingestion pipeline (Spec 035 §13).
/// Handles the formats that yield embeddable text without an external dependency:
/// the plain-text family (read directly) and Office Open XML / DOCX (parsed from the package's
/// <c>word/document.xml</c> with the BCL <see cref="ZipArchive"/> — no third-party library).
/// PDF and image/scanned content are routed to the <see cref="IDocumentOcrExtractor"/> hook;
/// when no OCR backend is wired, extraction <em>defers</em> (<see cref="ExtractedTextStatus.OcrRequired"/>)
/// rather than failing, so "index all documents" degrades gracefully (risk register §24).
/// </summary>
internal sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private readonly IDocumentOcrExtractor _ocr;
    private readonly ILogger<DocumentTextExtractor> _logger;

    public DocumentTextExtractor(IDocumentOcrExtractor ocr, ILogger<DocumentTextExtractor> logger)
    {
        _ocr = ocr;
        _logger = logger;
    }

    public async Task<DocumentTextExtractionResult> ExtractTextAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var normalized = contentType?.Trim() ?? string.Empty;

        if (IsPlainText(normalized))
        {
            using var reader = new StreamReader(
                content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return DocumentTextExtractionResult.Native(text);
        }

        if (string.Equals(normalized, DocxContentType, StringComparison.OrdinalIgnoreCase))
        {
            var text = await ExtractDocxTextAsync(content, cancellationToken);
            return string.IsNullOrWhiteSpace(text)
                ? DocumentTextExtractionResult.Unsupported()
                : DocumentTextExtractionResult.Native(text);
        }

        // PDF and images need OCR / document-intelligence. Route to the hook; defer if absent.
        if (string.Equals(normalized, "application/pdf", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            if (_ocr.IsAvailable)
            {
                return await _ocr.ExtractTextAsync(content, normalized, cancellationToken);
            }

            _logger.LogInformation(
                "Deferring text extraction for content type {ContentType}: no OCR adapter is wired " +
                "(Spec 035 §3 — OCR is a follow-up). The document remains pending re-ingestion.",
                normalized);
            return DocumentTextExtractionResult.OcrRequired();
        }

        return DocumentTextExtractionResult.Unsupported();
    }

    private static bool IsPlainText(string contentType) =>
        contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        || contentType is "application/json" or "application/xml" or "application/x-ndjson";

    /// <summary>
    /// Extracts visible text from a DOCX package using only the BCL. DOCX is a ZIP whose main part
    /// (<c>word/document.xml</c>) is WordprocessingML; we walk paragraphs (<c>w:p</c>) and emit the
    /// text of each run (<c>w:t</c>), honouring tabs (<c>w:tab</c>) and breaks (<c>w:br</c>/<c>w:cr</c>).
    /// </summary>
    private static async Task<string> ExtractDocxTextAsync(Stream content, CancellationToken cancellationToken)
    {
        // ZipArchive requires a seekable stream; blob read streams are forward-only, so buffer first.
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null)
        {
            return string.Empty;
        }

        await using var entryStream = entry.Open();
        var document = await XDocument.LoadAsync(entryStream, LoadOptions.None, cancellationToken);

        var builder = new StringBuilder();
        foreach (var paragraph in document.Descendants(W + "p"))
        {
            foreach (var node in paragraph.Descendants())
            {
                if (node.Name == W + "t")
                {
                    builder.Append(node.Value);
                }
                else if (node.Name == W + "tab")
                {
                    builder.Append('\t');
                }
                else if (node.Name == W + "br" || node.Name == W + "cr")
                {
                    builder.Append('\n');
                }
            }

            builder.Append('\n');
        }

        return builder.ToString().Trim();
    }
}

/// <summary>
/// Default no-op <see cref="IDocumentOcrExtractor"/> (Spec 035 §3/§26 open decision). Reports
/// that OCR is unavailable so the pipeline records image/PDF content as
/// <see cref="ExtractedTextStatus.OcrRequired"/> and leaves the document awaiting a backfill once a
/// real adapter (Azure AI Document Intelligence / Textract / open-source) is registered in its place.
/// </summary>
internal sealed class DeferredDocumentOcrExtractor : IDocumentOcrExtractor
{
    public bool IsAvailable => false;

    public Task<DocumentTextExtractionResult> ExtractTextAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
        => Task.FromResult(DocumentTextExtractionResult.OcrRequired());
}
