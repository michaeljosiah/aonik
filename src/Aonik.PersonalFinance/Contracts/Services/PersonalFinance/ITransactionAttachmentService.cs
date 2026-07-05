using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

public interface ITransactionAttachmentService
{
    Task<IReadOnlyList<TransactionAttachmentResponse>> GetAttachmentsAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);

    Task<TransactionAttachmentResponse> AddAttachmentAsync(
        Guid transactionId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAttachmentAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}
