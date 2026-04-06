using Aonik.Platform.Contracts.Models.Compliance;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.SharedKernel.Abstractions;
using Aonik.Platform.Contracts.Services.Compliance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Compliance;

public class ListDocumentsEndpoint : Endpoint<ListDocumentsRequest, PagedResult<DocumentListItem>>
{
    private readonly IDocumentService _documentService;

    public ListDocumentsEndpoint(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public override void Configure()
    {
        Get("/compliance/documents");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List compliance documents";
            s.Description = "Returns a paginated list of compliance documents with optional filtering.";
            s.Response(200, "Document list returned");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Compliance"));
    }

    public override async Task HandleAsync(ListDocumentsRequest req, CancellationToken ct)
    {
        var result = await _documentService.ListDocumentsAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
