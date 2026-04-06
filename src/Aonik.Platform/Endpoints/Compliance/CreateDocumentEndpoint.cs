using ApiCreateDocumentRequest = Aonik.Platform.Contracts.Api.Compliance.CreateDocumentRequest;
using ApiDocumentResponse = Aonik.Platform.Contracts.Api.Compliance.DocumentResponse;
using AppCreateDocumentRequest = Aonik.Platform.Contracts.Models.Compliance.CreateDocumentRequest;
using AppDocumentResponse = Aonik.Platform.Contracts.Models.Compliance.DocumentResponse;
using Aonik.Platform.Contracts.Services.Compliance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Compliance;

public class CreateDocumentEndpoint : Endpoint<ApiCreateDocumentRequest, ApiDocumentResponse>
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
        Summary(s =>
        {
            s.Summary = "Create a compliance document";
            s.Description = "Creates a new compliance document record with type, issuer, country, and expiry details.";
            s.Response(201, "Document created");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Compliance"));
    }

    public override async Task HandleAsync(ApiCreateDocumentRequest req, CancellationToken ct)
    {
        var result = await _documentService.CreateDocumentAsync(
            new AppCreateDocumentRequest(
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

    private static ApiDocumentResponse MapDocument(AppDocumentResponse response)
    {
        return new ApiDocumentResponse(
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
