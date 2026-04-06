using Aonik.Platform.Contracts.Api.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Add file metadata to a document";
            s.Description = "Attaches file metadata (storage location, content type, hash) to an existing compliance document.";
            s.Response(200, "File metadata added");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Compliance"));
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
