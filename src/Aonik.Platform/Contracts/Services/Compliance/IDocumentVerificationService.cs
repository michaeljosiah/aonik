using Aonik.Platform.Contracts.Models.Compliance;

namespace Aonik.Platform.Contracts.Services.Compliance;

/// <summary>
/// Compliance verification over documents (Spec 035 §12). Owns DocumentUsage / DocumentVerification;
/// the document itself lives in <c>Aonik.Documents</c> and is resolved by id via <c>IDocumentReader</c>.
/// Split out of the former combined <c>DocumentService</c>.
/// </summary>
public interface IDocumentVerificationService
{
    Task<DocumentUsageResponse> AddDocumentUsageAsync(
        AddDocumentUsageRequest request,
        CancellationToken cancellationToken = default);

    Task<DocumentVerificationResponse> AddDocumentVerificationAsync(
        AddDocumentVerificationRequest request,
        CancellationToken cancellationToken = default);
}
