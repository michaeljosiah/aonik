using Aonik.Api.Contracts.Compliance;
using Aonik.Application.Services.Compliance;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Compliance;

public class AddDocumentVerificationEndpoint : Endpoint<AddDocumentVerificationRequest, DocumentVerificationResponse>
{
    private readonly IDocumentService _documentService;

    public AddDocumentVerificationEndpoint(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public override void Configure()
    {
        Post("/compliance/document-usages/{id}/verifications");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(AddDocumentVerificationRequest req, CancellationToken ct)
    {
        var usageId = Route<Guid>("id");
        var result = await _documentService.AddDocumentVerificationAsync(
            new Application.Models.Compliance.AddDocumentVerificationRequest(
                usageId,
                req.Decision,
                req.DecisionReasonCode,
                req.DecisionNotes,
                req.VerifierType,
                req.VerifierId,
                req.AiRunId),
            ct);

        await Send.OkAsync(MapVerification(result), ct);
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
