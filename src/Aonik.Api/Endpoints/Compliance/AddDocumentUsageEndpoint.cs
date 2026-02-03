using Aonik.Api.Contracts.Compliance;
using Aonik.Application.Services.Compliance;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Compliance;

public class AddDocumentUsageEndpoint : Endpoint<AddDocumentUsageRequest, DocumentUsageResponse>
{
    private readonly IDocumentService _documentService;

    public AddDocumentUsageEndpoint(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public override void Configure()
    {
        Post("/compliance/documents/{id}/usages");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(AddDocumentUsageRequest req, CancellationToken ct)
    {
        var documentId = Route<Guid>("id");
        var result = await _documentService.AddDocumentUsageAsync(
            new Application.Models.Compliance.AddDocumentUsageRequest(
                documentId,
                req.OwnerPartyId,
                req.Purpose,
                req.RelatedEntityType,
                req.RelatedEntityId,
                req.Status,
                req.Notes),
            ct);

        await Send.OkAsync(MapUsage(result), ct);
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

    private static DocumentVerificationResponse MapVerification(
        Application.Models.Compliance.DocumentVerificationResponse response)
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
}
