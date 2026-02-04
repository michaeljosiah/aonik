using Aonik.Application.Models.Compliance;
using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Compliance;

public interface IDocumentService
{
    Task<PagedResult<DocumentListItem>> ListDocumentsAsync(
        ListDocumentsRequest request,
        CancellationToken cancellationToken = default);
    Task<DocumentResponse> CreateDocumentAsync(CreateDocumentRequest request, CancellationToken cancellationToken = default);
    Task<DocumentFileResponse> AddDocumentFileAsync(AddDocumentFileRequest request, CancellationToken cancellationToken = default);
    Task<DocumentUsageResponse> AddDocumentUsageAsync(AddDocumentUsageRequest request, CancellationToken cancellationToken = default);
    Task<DocumentVerificationResponse> AddDocumentVerificationAsync(
        AddDocumentVerificationRequest request,
        CancellationToken cancellationToken = default);
    Task<DocumentDetailsResponse?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
