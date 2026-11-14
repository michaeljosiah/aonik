using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;
using ImageMagick;

using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.Infrastructure.Storage;
using Aonik.Platform.Contracts.Models.ContactEnquiries;

namespace Aonik.Infrastructure.Documents;

/// <summary>Bounded local Tesseract/Poppler adapter behind the existing document extraction hook.</summary>
internal sealed partial class ProcessDocumentOcrExtractor(IOptions<DocumentOcrOptions> options, ClamAvScanner scanner) : IDocumentOcrExtractor
{
    private const int MaximumBytes = 10 * 1024 * 1024;
    private const int MaximumPages = 10;
    private const int MaximumTextCharacters = 100_000;
    private DocumentOcrOptions Settings => options.Value;
    public bool IsAvailable => Settings.Enabled && Available(Settings.TesseractPath);

    public async Task<DocumentTextExtractionResult> ExtractTextAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable) return DocumentTextExtractionResult.OcrRequired();
        if (Settings.TimeoutSeconds is < 1 or > 60 || !Regex.IsMatch(Settings.Language, "^[a-z]{3}(\\+[a-z]{3}){0,3}$", RegexOptions.NonBacktracking))
            throw new DocumentTextExtractionException("OcrConfigurationInvalid");
        bool pdf = contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
        bool png = contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase);
        bool jpeg = contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase);
        if (!pdf && !png && !jpeg) return DocumentTextExtractionResult.Unsupported();
        if (pdf && (!Available(Settings.PdfToTextPath) || !Available(Settings.PdfToPpmPath))) return DocumentTextExtractionResult.OcrRequired();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); budget.CancelAfter(TimeSpan.FromSeconds(Settings.TimeoutSeconds));
        byte[] bytes = await ReadContentAsync(content, budget.Token);
        if (pdf ? !bytes.AsSpan().StartsWith("%PDF-"u8) : png ? !bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) : bytes.Length < 3 || bytes[0] != 255 || bytes[1] != 216 || bytes[2] != 255)
            throw new DocumentTextExtractionException("DocumentSignatureMismatch");
        try { await scanner.ScanAsync(bytes, budget.Token); }
        catch (ContactImageValidationException) { throw new DocumentTextExtractionException("DocumentUnsafe"); }
        catch (ContactEnquiryUnavailableException) { throw new DocumentTextExtractionException("DocumentSafetyUnavailable"); }
        if (!pdf) await RequireImageBoundsAsync(bytes, budget.Token);
        string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        string directory = Directory.CreateTempSubdirectory("aonik-ocr-").FullName;
        try
        {
            string path = Path.Combine(directory, pdf ? "source.pdf" : png ? "source.png" : "source.jpg");
            await File.WriteAllBytesAsync(path, bytes, budget.Token);
            if (pdf)
            {
                ProcessOutput native = await RunAsync(Settings.PdfToTextPath, ["-f", "1", "-l", "11", "-layout", "-enc", "UTF-8", path, "-"], directory, budget.Token);
                int pages = native.Output.Count(character => character == '\f');
                if (pages > MaximumPages) throw new DocumentTextExtractionException("DocumentPageLimit");
                List<string> textPages = native.Output.Split('\f').ToList();
                if (textPages.Count > 1 && string.IsNullOrWhiteSpace(textPages[^1])) textPages.RemoveAt(textPages.Count - 1);
                if (textPages.All(page => page.Count(char.IsLetterOrDigit) >= 50))
                {
                    if (native.Output.Length > MaximumTextCharacters) throw new DocumentTextExtractionException("DocumentTextLimit");
                    string version = Version(await RunAsync(Settings.PdfToTextPath, ["-v"], directory, budget.Token));
                    return new(native.Output.Replace('\f', '\n'), ExtractedTextStatus.Native, new("Poppler", version, Math.Max(1, pages), null));
                }
                await RunAsync(Settings.PdfToPpmPath, ["-f", "1", "-l", "11", "-scale-to", "2400", "-png", path, Path.Combine(directory, "page")], directory, budget.Token);
            }
            string[] images = pdf ? Directory.GetFiles(directory, "page-*.png").OrderBy(PageNumber).ToArray() : [path];
            if (images.Length is 0 or > MaximumPages) throw new DocumentTextExtractionException("DocumentPageLimit");
            var text = new StringBuilder(); var confidences = new List<double>();
            foreach (string image in images)
            {
                var info = new FileInfo(image);
                if (info.Length > 30 * 1024 * 1024) throw new DocumentTextExtractionException("DocumentImageLimit");
                await using (FileStream rendered = File.OpenRead(image)) await RequireImageBoundsAsync(rendered, budget.Token);
                ProcessOutput result = await RunAsync(Settings.TesseractPath, [image, "stdout", "-l", Settings.Language, "--psm", "6", "tsv"], directory, budget.Token);
                (string pageText, double? confidence) = ReadTsv(result.Output);
                if (text.Length + pageText.Length > MaximumTextCharacters) throw new DocumentTextExtractionException("DocumentTextLimit");
                text.AppendLine(pageText); if (confidence.HasValue) confidences.Add(confidence.Value);
            }
            string engineVersion = Version(await RunAsync(Settings.TesseractPath, ["--version"], directory, budget.Token));
            return new(text.ToString().Trim(), ExtractedTextStatus.OcrDone, new("Tesseract", engineVersion, images.Length,
                confidences.Count == 0 ? null : confidences.Min()));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new DocumentTextExtractionException("OcrDeadlineExceeded"); }
        finally
        {
            string target = Path.GetFullPath(directory);
            if (target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target)) Directory.Delete(target, recursive: true);
        }
    }

    internal static (string Text, double? Confidence) ReadTsv(string output)
    {
        var text = new StringBuilder(); string? previousLine = null; double sum = 0; int words = 0;
        foreach (string row in output.Split('\n').Skip(1))
        {
            if (string.IsNullOrWhiteSpace(row)) continue;
            string[] fields = row.TrimEnd('\r').Split('\t', 12);
            if (fields.Length != 12 || fields[0] != "5" || string.IsNullOrWhiteSpace(fields[11])) continue;
            if (!double.TryParse(fields[10], NumberStyles.Float, CultureInfo.InvariantCulture, out double confidence)
                || !double.IsFinite(confidence) || confidence is < 0 or > 100) throw new DocumentTextExtractionException("OcrResponseInvalid");
            string line = string.Join(':', fields.Skip(1).Take(4));
            if (text.Length > 0) text.Append(line == previousLine ? ' ' : '\n');
            text.Append(fields[11]); previousLine = line; sum += confidence; words++;
            if (text.Length > MaximumTextCharacters || words > 20_000) throw new DocumentTextExtractionException("DocumentTextLimit");
        }
        return (text.ToString(), words == 0 ? null : sum / words / 100);
    }

    private static string Version(ProcessOutput output)
    {
        string source = string.IsNullOrWhiteSpace(output.Output) ? output.Error : output.Output;
        string version = source.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "unknown";
        return version[..Math.Min(200, version.Length)];
    }
    private static bool Available(string path) => Path.IsPathFullyQualified(path) && File.Exists(path);
    private static int PageNumber(string path) => int.TryParse(Path.GetFileNameWithoutExtension(path).AsSpan("page-".Length),
        NumberStyles.None, CultureInfo.InvariantCulture, out int page) && page is > 0 and <= MaximumPages + 1
            ? page : throw new DocumentTextExtractionException("OcrResponseInvalid");
    private static async Task<byte[]> ReadContentAsync(Stream content, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream(); byte[] buffer = new byte[8192]; int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > MaximumBytes) throw new DocumentTextExtractionException("DocumentSizeLimit");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        if (output.Length == 0) throw new DocumentTextExtractionException("DocumentEmpty");
        return output.ToArray();
    }
    private static async Task RequireImageBoundsAsync(byte[] bytes, CancellationToken cancellationToken)
    { using var stream = new MemoryStream(bytes, writable: false); await RequireImageBoundsAsync(stream, cancellationToken); }
    private static Task RequireImageBoundsAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            IMagickImageInfo[] frames = MagickImageInfo.ReadCollection(stream, new MagickReadSettings { FrameCount = 2 }).Take(2).ToArray();
            if (frames.Length != 1) throw new DocumentTextExtractionException("DocumentImageLimit");
            IMagickImageInfo info = frames[0];
            if (info.Width is < 1 or > 12_000 || info.Height is < 1 or > 12_000 || (long)info.Width * info.Height > 25_000_000)
                throw new DocumentTextExtractionException("DocumentImageLimit");
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
        catch (MagickException)
        { throw new DocumentTextExtractionException("DocumentImageInvalid"); }
    }
}
