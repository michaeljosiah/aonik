using Aonik.SharedKernel.Abstractions.Documents;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Documents.Endpoints;

/// <summary>Upload a file's bytes into an existing document (multipart). Spec 035 §7/§13.</summary>
public sealed class UploadDocumentFileEndpoint : EndpointWithoutRequest<DocumentFileDto>
{
    private readonly IDocumentWriter _writer;

    public UploadDocumentFileEndpoint(IDocumentWriter writer) => _writer = writer;

    public override void Configure()
    {
        Post("/documents/{id}/files");
        Policies("UserPolicy");
        AllowFileUploads();
        Summary(s =>
        {
            s.Summary = "Upload a file to a document";
            s.Description = "Uploads a binary file and attaches it to an existing document via multipart form.";
            s.Response(200, "File uploaded");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var documentId = Route<Guid>("id");

        if (Files.Count == 0)
        {
            await SendValidationAsync("A document file is required.", ct);
            return;
        }

        var file = Files[0];
        if (file.Length == 0)
        {
            await SendValidationAsync("The document file is empty.", ct);
            return;
        }

        var form = await HttpContext.Request.ReadFormAsync(ct);
        var command = new UploadFileCommand(
            documentId,
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            ParseInt(form["pageIndex"]),
            Normalize(form["side"]),
            ParseDate(form["capturedAt"]),
            Normalize(form["capturedBy"]),
            Normalize(form["metadataJson"]));

        await using var stream = file.OpenReadStream();
        var result = await _writer.UploadFileAsync(command, stream, ct);
        await Send.OkAsync(result, ct);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int? ParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private static DateTime? ParseDate(string? value)
        => DateTime.TryParse(value, out var parsed) ? parsed : null;

    private async Task SendValidationAsync(string message, CancellationToken ct)
    {
        HttpContext.Response.StatusCode = 422;
        await HttpContext.Response.WriteAsJsonAsync(new { error = message }, ct);
    }
}
