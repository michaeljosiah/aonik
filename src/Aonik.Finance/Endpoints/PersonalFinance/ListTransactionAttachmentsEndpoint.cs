using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var transactionId = Route<Guid>("transactionId");
        var attachments = await _attachmentService.GetAttachmentsAsync(transactionId, ct);
        await Send.OkAsync(attachments, ct);
    }
}
