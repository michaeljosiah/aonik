using Aonik.Platform.Contracts.Models.Compliance;
using Aonik.Platform.Contracts.Models.Identity;

namespace Aonik.Platform.Contracts.Services.Compliance;

public interface IDocumentService
{
    Task<PagedResult<DocumentListItem>> ListDocumentsAsync(
        ListDocumentsRequest request,
        CancellationToken cancellationToken = default);
    Task<DocumentResponse> CreateDocumentAsync(CreateDocumentRequest request, CancellationToken cancellationToken = default);
    Task<DocumentFileResponse> AddDocumentFileAsync(AddDocumentFileRequest request, CancellationToken cancellationToken = default);
    Task<DocumentFileResponse> UploadDocumentFileAsync(
        UploadDocumentFileRequest request,
        Stream fileStream,
        CancellationToken cancellationToken = default);
    Task<DocumentUsageResponse> AddDocumentUsageAsync(AddDocumentUsageRequest request, CancellationToken cancellationToken = default);
    Task<DocumentVerificationResponse> AddDocumentVerificationAsync(
        AddDocumentVerificationRequest request,
        CancellationToken cancellationToken = default);
    Task<DocumentDetailsResponse?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
