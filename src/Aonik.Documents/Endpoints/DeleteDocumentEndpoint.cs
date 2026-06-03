using Aonik.SharedKernel.Abstractions.Documents;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Documents.Endpoints;

/// <summary>
/// Erase a document and its files (right-to-erasure, Spec 035 §15): purges the document's vectors,
/// removes its blob object(s), soft-deletes the rows, and emits <c>DocumentDeletedEvent</c> (which
/// Compliance handles by marking dependent usages <c>Expired</c>). Admin-gated; customer-lifecycle
/// and approval-gated agent erasure flows invoke <see cref="IDocumentWriter.DeleteDocumentAsync"/>
/// directly under their own authorisation.
/// </summary>
public sealed class DeleteDocumentEndpoint : EndpointWithoutRequest
{
    private readonly IDocumentWriter _writer;
    private readonly IDocumentReader _reader;

    public DeleteDocumentEndpoint(IDocumentWriter writer, IDocumentReader reader)
    {
        _writer = writer;
        _reader = reader;
    }

    public override void Configure()
    {
        Delete("/documents/{id}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Delete (erase) a document";
            s.Description =
                "Purges the document's vectors and blob objects, soft-deletes its rows, and emits " +
                "DocumentDeletedEvent so Compliance marks dependent usages Expired. Admin-only.";
            s.Response(204, "Document erased");
            s.Response(401, "Not authenticated");
            s.Response(403, "Not authorised");
            s.Response(404, "Document not found");
        });
        Options(x => x.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        // 404 before erasing so a missing/cross-tenant id is a clean Not Found, not a 500. The
        // reader is tenant-scoped, so a document in another tenant is invisible here.
        var existing = await _reader.GetDocumentAsync(id, ct);
        if (existing is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await _writer.DeleteDocumentAsync(id, ct);
        await Send.NoContentAsync(ct);
    }
}
