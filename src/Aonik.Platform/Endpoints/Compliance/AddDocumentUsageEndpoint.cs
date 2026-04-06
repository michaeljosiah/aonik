using Aonik.Platform.Contracts.Api.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Compliance;

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
        Summary(s =>
        {
            s.Summary = "Add usage record to a document";
            s.Description = "Records a new usage of a compliance document, linking it to a party, purpose, and related entity.";
            s.Response(200, "Usage record added");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Compliance"));
    }

    public override async Task HandleAsync(AddDocumentUsageRequest req, CancellationToken ct)
    {
        var documentId = Route<Guid>("id");
        var result = await _documentService.AddDocumentUsageAsync(
            new Aonik.Platform.Contracts.Models.Compliance.AddDocumentUsageRequest(
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

    private static DocumentUsageResponse MapUsage(Aonik.Platform.Contracts.Models.Compliance.DocumentUsageResponse response)
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
        Aonik.Platform.Contracts.Models.Compliance.DocumentVerificationResponse response)
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
