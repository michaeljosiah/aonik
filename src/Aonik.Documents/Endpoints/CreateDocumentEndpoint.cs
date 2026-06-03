using Aonik.SharedKernel.Abstractions.Documents;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Documents.Endpoints;

/// <summary>Create a generic document (customer or admin). Spec 035 §7 — re-homed from /compliance/documents.</summary>
public sealed class CreateDocumentEndpoint : Endpoint<CreateDocumentCommand, DocumentDto>
{
    private readonly IDocumentWriter _writer;

    public CreateDocumentEndpoint(IDocumentWriter writer) => _writer = writer;

    public override void Configure()
    {
        Post("/documents");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Create a document";
            s.Description = "Creates a generic document record (no compliance usage required). Customer-accessible.";
            s.Response(201, "Document created");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Documents"));
    }

    public override async Task HandleAsync(CreateDocumentCommand req, CancellationToken ct)
    {
        var result = await _writer.CreateDocumentAsync(req, ct);
        await Send.CreatedAtAsync<GetDocumentEndpoint>(
            routeValues: new { id = result.DocumentId },
            responseBody: result,
            cancellation: ct);
    }
}
