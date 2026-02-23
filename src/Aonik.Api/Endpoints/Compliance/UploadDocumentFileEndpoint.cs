using Aonik.Api.Contracts.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Compliance;

public class UploadDocumentFileEndpoint : EndpointWithoutRequest<DocumentFileResponse>
{
    private readonly IDocumentService _documentService;

    public UploadDocumentFileEndpoint(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public override void Configure()
    {
        Post("/compliance/documents/{id}/files/upload");
        Policies("AdminUserPolicy");
        AllowFileUploads();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var documentId = Route<Guid>("id");

        if (Files.Count == 0)
        {
            await SendValidationErrorAsync("Document file is required.", ct);
            return;
        }

        var file = Files[0];
        if (file.Length == 0)
        {
            await SendValidationErrorAsync("Document file is empty.", ct);
            return;
        }

        var form = await HttpContext.Request.ReadFormAsync(ct);
        var pageIndex = ParseOptionalInt(form["pageIndex"], out var pageIndexError);
        if (pageIndexError != null)
        {
            await SendValidationErrorAsync(pageIndexError, ct);
            return;
        }

        var capturedAt = ParseOptionalDateTime(form["capturedAt"], out var capturedAtError);
        if (capturedAtError != null)
        {
            await SendValidationErrorAsync(capturedAtError, ct);
            return;
        }

        var request = new Aonik.Platform.Contracts.Models.Compliance.UploadDocumentFileRequest(
            documentId,
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            pageIndex,
            Normalize(form["side"]),
            capturedAt,
            Normalize(form["capturedBy"]),
            Normalize(form["metadataJson"]));

        await using var stream = file.OpenReadStream();
        var result = await _documentService.UploadDocumentFileAsync(request, stream, ct);

        await Send.OkAsync(MapFile(result), ct);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static int? ParseOptionalInt(string? value, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, out var parsed))
        {
            error = "Page index must be a whole number.";
            return null;
        }

        return parsed;
    }

    private static DateTime? ParseOptionalDateTime(string? value, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTime.TryParse(value, out var parsed))
        {
            error = "Captured at must be a valid date/time value.";
            return null;
        }

        return parsed;
    }

    private async Task SendValidationErrorAsync(string message, CancellationToken ct)
    {
        HttpContext.Response.StatusCode = 422;
        await HttpContext.Response.WriteAsJsonAsync(new { error = message }, ct);
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
