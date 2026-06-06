using Aonik.SharedKernel.Abstractions.Documents;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Documents.Endpoints;

/// <summary>List the files attached to a document (metadata only). Spec 035 §7/§11.</summary>
public sealed class ListDocumentFilesEndpoint : EndpointWithoutRequest<IReadOnlyList<DocumentFileDto>>
{
    private readonly IDocumentReader _reader;

    public ListDocumentFilesEndpoint(IDocumentReader reader) => _reader = reader;

    public override void Configure()
    {
        Get("/documents/{id}/files");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List a document's files";
            s.Description =
                "Returns the files attached to a document, ordered by page index. Owner-scoped: a " +
                "customer only sees files of their own documents (an unauthorized or unknown id " +
                "yields an empty list rather than revealing the document's existence).";
            s.Response(200, "Document files returned");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var files = await _reader.GetFilesAsync(id, ct);
        await Send.OkAsync(files, ct);
    }
}
