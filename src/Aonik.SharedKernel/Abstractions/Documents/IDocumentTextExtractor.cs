namespace Aonik.SharedKernel.Abstractions.Documents;

/// <summary>
/// Result of attempting to extract embeddable text from a document file's bytes.
/// <see cref="Status"/> tells the ingestion pipeline whether the text is ready to chunk and
/// embed (<see cref="ExtractedTextStatus.Native"/> / <see cref="ExtractedTextStatus.OcrDone"/>)
/// or must be deferred (<see cref="ExtractedTextStatus.OcrRequired"/>) or skipped
/// (<see cref="ExtractedTextStatus.Unsupported"/>). <see cref="Text"/> is empty unless the
/// status indicates text is available.
/// </summary>
public sealed record DocumentTextExtractionResult(string Text, ExtractedTextStatus Status)
{
    /// <summary>True when embeddable text was produced and is ready to chunk + embed.</summary>
    public bool HasText => Status is ExtractedTextStatus.Native or ExtractedTextStatus.OcrDone
        && !string.IsNullOrWhiteSpace(Text);

    public static DocumentTextExtractionResult Native(string text) => new(text, ExtractedTextStatus.Native);
    public static DocumentTextExtractionResult OcrRequired() => new(string.Empty, ExtractedTextStatus.OcrRequired);
    public static DocumentTextExtractionResult Unsupported() => new(string.Empty, ExtractedTextStatus.Unsupported);
}

/// <summary>
/// Extracts embeddable plain text from a document file's bytes for the RAG ingestion pipeline
/// (Spec 035 §13). The contract lives in SharedKernel so <c>Aonik.Documents</c> can drive
/// extraction without referencing Infrastructure; the implementation (native text formats now,
/// PDF/image routed through the <see cref="IDocumentOcrExtractor"/> hook) stays in
/// <c>Aonik.Infrastructure</c>. Native text and Office Open XML (DOCX) are handled directly;
/// anything that needs OCR defers rather than failing, so "index all documents" degrades
/// gracefully until an OCR adapter lands.
/// </summary>
public interface IDocumentTextExtractor
{
    Task<DocumentTextExtractionResult> ExtractTextAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optical-character-recognition / document-intelligence hook for image-only and scanned
/// content (Spec 035 §10/§13, out-of-scope §3). The interface is defined now; a real adapter
/// (Azure AI Document Intelligence, Textract, or open-source) is a clean follow-up. The default
/// <c>Deferred</c> implementation reports that OCR is unavailable, so the pipeline records the
/// file as <see cref="ExtractedTextStatus.OcrRequired"/> and leaves the document awaiting a
/// later backfill rather than failing ingestion.
/// </summary>
public interface IDocumentOcrExtractor
{
    /// <summary>
    /// Attempts to extract text from image/scanned bytes. Returns a result whose status is
    /// <see cref="ExtractedTextStatus.OcrDone"/> with text on success, or
    /// <see cref="ExtractedTextStatus.OcrRequired"/> when OCR is not (yet) available.
    /// </summary>
    Task<DocumentTextExtractionResult> ExtractTextAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Whether a real OCR backend is wired. The default deferred hook returns false.</summary>
    bool IsAvailable { get; }
}
