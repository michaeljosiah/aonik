using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

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
