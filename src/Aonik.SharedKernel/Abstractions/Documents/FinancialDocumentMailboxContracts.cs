namespace Aonik.SharedKernel.Abstractions.Documents;

public sealed record FinancialDocumentAttachment(FinancialDocumentSource Source, string FileName, string ContentType, byte[] Bytes);

/// <summary>Only approved mailbox bindings may retrieve sources for automatic document admission.</summary>
public interface IFinancialDocumentMailboxReader
{
    Task<IReadOnlyList<FinancialDocumentAttachment>> ReadAsync(Guid bindingId, int revision, string messageId,
        CancellationToken cancellationToken = default);
}

public interface IFinancialDocumentMailboxIntake
{
    Task<IReadOnlyList<Guid>> AdmitAsync(Guid bindingId, int revision, string messageId, CancellationToken cancellationToken = default);
}
