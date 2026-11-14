using Aonik.Infrastructure.Storage;
using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.SharedKernel.Abstractions.Documents;

namespace Aonik.Infrastructure.Documents;

internal sealed class DocumentSafetyScanner(ClamAvScanner scanner) : IDocumentSafetyScanner
{
    public async Task ScanAsync(byte[] content, CancellationToken cancellationToken = default)
    {
        if (content.Length is 0 or > 10 * 1024 * 1024) throw new DocumentTextExtractionException("DocumentSizeLimit");
        try { await scanner.ScanAsync(content, cancellationToken); }
        catch (ContactImageValidationException) { throw new DocumentTextExtractionException("DocumentUnsafe"); }
        catch (ContactEnquiryUnavailableException) { throw new DocumentTextExtractionException("DocumentSafetyUnavailable"); }
    }
}
