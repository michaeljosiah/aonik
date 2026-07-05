using Microsoft.AspNetCore.Http;

using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class UploadTransactionAttachmentEndpoint
    : EndpointWithoutRequest<TransactionAttachmentResponse>
{
    private readonly ITransactionAttachmentService _attachmentService;

    public UploadTransactionAttachmentEndpoint(ITransactionAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    public override void Configure()
    {
        Post("/personal-finance/spending/transactions/{transactionId}/attachments");
        Policies("UserPolicy");
        AllowFileUploads();
        Summary(s =>
        {
            s.Summary = "Upload a transaction attachment";
            s.Description = "Uploads a file attachment (receipt, invoice, photo) and associates it with a personal transaction.";
            s.Response(201, "Attachment uploaded successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Transaction not found");
            s.Response(422, "Validation error or missing file");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (Files.Count == 0)
        {
            await SendValidationErrorAsync("A file is required.", ct);
            return;
        }

        var file = Files[0];
        if (file.Length == 0)
        {
            await SendValidationErrorAsync("File is empty.", ct);
            return;
        }

        var transactionId = Route<Guid>("transactionId");
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        try
        {
            await using var stream = file.OpenReadStream();
            var response = await _attachmentService.AddAttachmentAsync(
                transactionId,
                stream,
                file.FileName,
                contentType,
                ct);

            await Send.CreatedAtAsync<ListTransactionAttachmentsEndpoint>(
                routeValues: new { transactionId },
                responseBody: response,
                cancellation: ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }

    private async Task SendValidationErrorAsync(string message, CancellationToken ct)
    {
        HttpContext.Response.StatusCode = 422;
        await HttpContext.Response.WriteAsJsonAsync(new { error = message }, ct);
    }
}
