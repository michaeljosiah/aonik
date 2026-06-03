using Aonik.SharedKernel.Abstractions.Documents;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Documents.Endpoints;

/// <summary>Get a generic document's metadata by id. Spec 035 §7.</summary>
public sealed class GetDocumentEndpoint : EndpointWithoutRequest<DocumentDto>
{
    private readonly IDocumentReader _reader;

    public GetDocumentEndpoint(IDocumentReader reader) => _reader = reader;

    public override void Configure()
    {
        Get("/documents/{id}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a document by id";
            s.Response(200, "Document returned");
            s.Response(401, "Not authenticated");
            s.Response(404, "Document not found");
        });
        Options(x => x.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _reader.GetDocumentAsync(id, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
