using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Documents.Endpoints;

/// <summary>List generic documents in the current tenant (filtered/paged). Spec 035 §7.</summary>
public sealed class ListDocumentsEndpoint : Endpoint<ListDocumentsQuery, PagedResult<DocumentListItem>>
{
    private readonly IDocumentReader _reader;

    public ListDocumentsEndpoint(IDocumentReader reader) => _reader = reader;

    public override void Configure()
    {
        Get("/documents");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List documents";
            s.Description = "Returns a paginated list of documents with optional filtering.";
            s.Response(200, "Document list returned");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Documents"));
    }

    public override async Task HandleAsync(ListDocumentsQuery req, CancellationToken ct)
    {
        var result = await _reader.ListDocumentsAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
