using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class DeleteTransactionAttachmentEndpoint : EndpointWithoutRequest
{
    private readonly ITransactionAttachmentService _attachmentService;

    public DeleteTransactionAttachmentEndpoint(ITransactionAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    public override void Configure()
    {
        Delete("/personal-finance/spending/attachments/{attachmentId}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Delete a transaction attachment";
            s.Description = "Permanently removes a file attachment from a personal transaction.";
            s.Response(204, "Attachment deleted successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Attachment not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var attachmentId = Route<Guid>("attachmentId");
            await _attachmentService.DeleteAttachmentAsync(attachmentId, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }
}
