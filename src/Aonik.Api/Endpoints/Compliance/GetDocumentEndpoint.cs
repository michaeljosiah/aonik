using Aonik.Api.Contracts.Compliance;
using Aonik.Application.Services.Compliance;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Compliance;

public class GetDocumentEndpoint : EndpointWithoutRequest<DocumentDetailsResponse>
{
    private readonly IDocumentService _documentService;

    public GetDocumentEndpoint(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public override void Configure()
    {
        Get("/compliance/documents/{id}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var documentId = Route<Guid>("id");
        var result = await _documentService.GetDocumentAsync(documentId, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(MapDetails(result), ct);
    }

    private static DocumentDetailsResponse MapDetails(Application.Models.Compliance.DocumentDetailsResponse response)
    {
        return new DocumentDetailsResponse(
            MapDocument(response.Document),
            response.Files.Select(MapFile).ToList(),
            response.Usages.Select(MapUsage).ToList(),
            response.Versions.Select(MapVersion).ToList());
    }

    private static DocumentResponse MapDocument(Application.Models.Compliance.DocumentResponse response)
    {
        return new DocumentResponse(
            response.DocumentId,
            response.OwnerPartyId,
            response.DocumentType,
            response.Status,
            response.IssuedOn,
            response.ExpiresOn,
            response.IssuerName,
            response.CountryCode,
            response.ReferenceNumber,
            response.Tags,
            response.AttributesJson,
            response.CreatedAt,
            response.UpdatedAt);
    }

    private static DocumentFileResponse MapFile(Application.Models.Compliance.DocumentFileResponse response)
    {
        return new DocumentFileResponse(
            response.DocumentFileId,
            response.DocumentId,
            response.StorageProvider,
            response.StorageContainer,
            response.StorageKey,
            response.ContentType,
            response.FileName,
            response.FileSizeBytes,
            response.Sha256,
            response.PageIndex,
            response.Side,
            response.CapturedAt,
            response.CapturedBy,
            response.MetadataJson,
            response.CreatedAt);
    }

    private static DocumentUsageResponse MapUsage(Application.Models.Compliance.DocumentUsageResponse response)
    {
        return new DocumentUsageResponse(
            response.DocumentUsageId,
            response.DocumentId,
            response.OwnerPartyId,
            response.Purpose,
            response.RelatedEntityType,
            response.RelatedEntityId,
            response.Status,
            response.VerifiedByUserId,
            response.VerifiedAt,
            response.Notes,
            response.Verifications.Select(MapVerification).ToList(),
            response.CreatedAt,
            response.UpdatedAt);
    }

    private static DocumentVerificationResponse MapVerification(Application.Models.Compliance.DocumentVerificationResponse response)
    {
        return new DocumentVerificationResponse(
            response.DocumentVerificationId,
            response.DocumentUsageId,
            response.Decision,
            response.DecisionReasonCode,
            response.DecisionNotes,
            response.VerifierType,
            response.VerifierId,
            response.AiRunId,
            response.CreatedAt);
    }

    private static DocumentVersionResponse MapVersion(Application.Models.Compliance.DocumentVersionResponse response)
    {
        return new DocumentVersionResponse(
            response.DocumentVersionId,
            response.DocumentId,
            response.Version,
            response.Status,
            response.SubmittedAt,
            response.DecisionedAt,
            response.DecisionReason,
            response.CreatedAt,
            response.UpdatedAt);
    }
}
