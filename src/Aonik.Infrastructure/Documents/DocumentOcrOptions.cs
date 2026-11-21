namespace Aonik.Infrastructure.Documents;

public sealed class DocumentOcrOptions
{
    public const string SectionName = "Documents:Ocr";
    public bool Enabled { get; set; }
    public string TesseractPath { get; set; } = OperatingSystem.IsWindows() ? "" : "/usr/bin/tesseract";
    public string PdfToTextPath { get; set; } = OperatingSystem.IsWindows() ? "" : "/usr/bin/pdftotext";
    public string PdfToPpmPath { get; set; } = OperatingSystem.IsWindows() ? "" : "/usr/bin/pdftoppm";
    public string Language { get; set; } = "eng";
    public int TimeoutSeconds { get; set; } = 60;
}
