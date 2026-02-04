using Aonik.Application.Models.Compliance;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Compliance;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Compliance;

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
    }

    public override async Task HandleAsync(ListDocumentsRequest req, CancellationToken ct)
    {
        var result = await _documentService.ListDocumentsAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
