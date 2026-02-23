using Aonik.Platform.Contracts.Api.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Compliance;

public class AddDocumentFileEndpoint : Endpoint<AddDocumentFileRequest, DocumentFileResponse>
{
    private readonly IDocumentService _documentService;

    public AddDocumentFileEndpoint(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public override void Configure()
    {
        Post("/compliance/documents/{id}/files");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(AddDocumentFileRequest req, CancellationToken ct)
    {
        var documentId = Route<Guid>("id");
        var result = await _documentService.AddDocumentFileAsync(
            new Aonik.Platform.Contracts.Models.Compliance.AddDocumentFileRequest(
                documentId,
                req.StorageProvider,
                req.StorageContainer,
                req.StorageKey,
                req.ContentType,
                req.FileName,
                req.FileSizeBytes,
                req.Sha256,
                req.PageIndex,
                req.Side,
                req.CapturedAt,
                req.CapturedBy,
                req.MetadataJson),
            ct);

        await Send.OkAsync(MapFile(result), ct);
    }

    private static DocumentFileResponse MapFile(Aonik.Platform.Contracts.Models.Compliance.DocumentFileResponse response)
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
}
