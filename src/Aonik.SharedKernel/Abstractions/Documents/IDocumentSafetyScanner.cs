namespace Aonik.SharedKernel.Abstractions.Documents;

public interface IDocumentSafetyScanner
{
    Task ScanAsync(byte[] content, CancellationToken cancellationToken = default);
}
