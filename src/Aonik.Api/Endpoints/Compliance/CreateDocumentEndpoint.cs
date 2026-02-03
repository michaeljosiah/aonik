using Aonik.Api.Contracts.Compliance;
using Aonik.Application.Models.Compliance;
using Aonik.Application.Services.Compliance;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Compliance;

public class CreateDocumentEndpoint : Endpoint<CreateDocumentRequest, DocumentResponse>
{
    private readonly IDocumentService _documentService;

    public CreateDocumentEndpoint(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public override void Configure()
    {
        Post("/compliance/documents");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreateDocumentRequest req, CancellationToken ct)
    {
        var result = await _documentService.CreateDocumentAsync(
            new Application.Models.Compliance.CreateDocumentRequest(
                req.OwnerPartyId,
                req.DocumentType,
                req.Status,
                req.IssuedOn,
                req.ExpiresOn,
                req.IssuerName,
                req.CountryCode,
                req.ReferenceNumber,
                req.Tags,
                req.AttributesJson),
            ct);

        await Send.CreatedAtAsync<GetDocumentEndpoint>(
            routeValues: new { id = result.DocumentId },
            responseBody: MapDocument(result),
            cancellation: ct);
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
}
