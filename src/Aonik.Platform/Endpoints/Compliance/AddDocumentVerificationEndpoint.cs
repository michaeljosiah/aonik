using Aonik.Platform.Contracts.Api.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Compliance;

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
        Summary(s =>
        {
            s.Summary = "Add verification to a document usage";
            s.Description = "Records a verification decision (approve, reject, etc.) against a document usage record.";
            s.Response(200, "Verification recorded");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Compliance"));
    }

    public override async Task HandleAsync(AddDocumentVerificationRequest req, CancellationToken ct)
    {
        var usageId = Route<Guid>("id");
        var result = await _documentService.AddDocumentVerificationAsync(
            new Aonik.Platform.Contracts.Models.Compliance.AddDocumentVerificationRequest(
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
