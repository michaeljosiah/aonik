using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class ListTransactionAttachmentsEndpoint
    : EndpointWithoutRequest<IReadOnlyList<TransactionAttachmentResponse>>
{
    private readonly ITransactionAttachmentService _attachmentService;

    public ListTransactionAttachmentsEndpoint(ITransactionAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    public override void Configure()
    {
        Get("/personal-finance/spending/transactions/{transactionId}/attachments");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List transaction attachments";
            s.Description = "Returns all file attachments (receipts, invoices, etc.) associated with a specific personal transaction.";
            s.Response(200, "Attachments returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var transactionId = Route<Guid>("transactionId");
        var attachments = await _attachmentService.GetAttachmentsAsync(transactionId, ct);
        await Send.OkAsync(attachments, ct);
    }
}
