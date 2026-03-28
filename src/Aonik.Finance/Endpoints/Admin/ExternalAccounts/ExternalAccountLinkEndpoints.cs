using Aonik.Finance.Contracts.Models.ExternalAccounts;
using Aonik.Finance.Contracts.Services.ExternalAccounts;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Admin.ExternalAccounts;

internal sealed class ListExternalAccountConnectionsRequest
{
    public bool IncludeDisconnected { get; set; }
}

internal sealed class CreateExternalAccountLinkSessionEndpoint : Endpoint<CreateExternalAccountLinkSessionRequest, ExternalAccountLinkSessionResponse>
{
    private readonly IExternalAccountLinkService _service;

    public CreateExternalAccountLinkSessionEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/external-accounts/connections/sessions");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CreateExternalAccountLinkSessionRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.CreateSessionAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

internal sealed class ExchangeExternalAccountLinkSessionEndpoint : Endpoint<ExchangeExternalAccountLinkSessionRequest, ExternalAccountLinkExchangeResponse>
{
    private readonly IExternalAccountLinkService _service;

    public ExchangeExternalAccountLinkSessionEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/external-accounts/connections/exchanges");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ExchangeExternalAccountLinkSessionRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.ExchangeSessionAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

internal sealed class ListExternalAccountConnectionsEndpoint : Endpoint<ListExternalAccountConnectionsRequest, IReadOnlyList<ExternalAccountConnectionResponse>>
{
    private readonly IExternalAccountLinkService _service;

    public ListExternalAccountConnectionsEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/external-accounts/connections");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ListExternalAccountConnectionsRequest req, CancellationToken ct)
    {
        var response = await _service.ListConnectionsAsync(req.IncludeDisconnected, ct);
        await Send.OkAsync(response, ct);
    }
}

internal sealed class RefreshExternalAccountConnectionEndpoint : EndpointWithoutRequest<ExternalAccountLinkActionResponse>
{
    private readonly IExternalAccountLinkService _service;

    public RefreshExternalAccountConnectionEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/external-accounts/connections/{id}/refresh");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var id = Route<Guid>("id");
            var response = await _service.RefreshConnectionAsync(id, ct);
            if (response == null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            await Send.OkAsync(new ExternalAccountLinkActionResponse("refresh", response), ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

internal sealed class DisconnectExternalAccountConnectionEndpoint : EndpointWithoutRequest<ExternalAccountLinkActionResponse>
{
    private readonly IExternalAccountLinkService _service;

    public DisconnectExternalAccountConnectionEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/external-accounts/connections/{id}/disconnect");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var response = await _service.DisconnectConnectionAsync(id, ct);
        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new ExternalAccountLinkActionResponse("disconnect", response), ct);
    }
}

internal sealed class SyncExternalAccountTransactionsEndpoint : EndpointWithoutRequest<ExternalAccountTransactionSyncResponse>
{
    private readonly IExternalAccountLinkService _service;

    public SyncExternalAccountTransactionsEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/external-accounts/connections/{id}/transactions/sync");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var id = Route<Guid>("id");
            var response = await _service.SyncConnectionTransactionsAsync(id, ct);
            if (response == null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

internal sealed class ListExternalAccountTransactionsRequest
{
    public Guid? ExternalAccountId { get; set; }
    public Guid? ConnectionId { get; set; }
    public string? ReconciliationStatus { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

internal sealed class ListExternalAccountTransactionsEndpoint : Endpoint<ListExternalAccountTransactionsRequest, PagedResult<ExternalAccountTransactionResponse>>
{
    private readonly IExternalAccountLinkService _service;

    public ListExternalAccountTransactionsEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/external-accounts/transactions");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ListExternalAccountTransactionsRequest req, CancellationToken ct)
    {
        var response = await _service.ListTransactionsAsync(
            new Contracts.Models.ExternalAccounts.ListExternalAccountTransactionsRequest(
                req.ExternalAccountId,
                req.ConnectionId,
                req.ReconciliationStatus,
                req.From,
                req.To,
                req.PageNumber,
                req.PageSize),
            ct);
        await Send.OkAsync(response, ct);
    }
}

internal sealed class PlaidExternalAccountWebhookEndpoint : Endpoint<PlaidExternalAccountWebhookRequest, ExternalAccountLinkWebhookResponse>
{
    private readonly IExternalAccountLinkService _service;

    public PlaidExternalAccountWebhookEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/external-accounts/webhooks/plaid");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PlaidExternalAccountWebhookRequest req, CancellationToken ct)
    {
        await _service.ProcessPlaidWebhookAsync(req, ct);
        await Send.OkAsync(new ExternalAccountLinkWebhookResponse("accepted"), ct);
    }
}

// ── Manual Account CRUD ──────────────────────────────────────────

internal sealed class CreateExternalAccountEndpoint : Endpoint<CreateExternalAccountRequest, ExternalAccountResponse>
{
    private readonly IExternalAccountLinkService _service;

    public CreateExternalAccountEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/external-accounts");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CreateExternalAccountRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.CreateAccountAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

internal sealed class ListExternalAccountsEndpoint : EndpointWithoutRequest<IReadOnlyList<ExternalAccountResponse>>
{
    private readonly IExternalAccountLinkService _service;

    public ListExternalAccountsEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/external-accounts");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _service.ListAccountsAsync(ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Manual Transaction CRUD ──────────────────────────────────────

internal sealed class CreateExternalAccountTransactionEndpoint : Endpoint<CreateExternalAccountTransactionRequest, ExternalAccountTransactionResponse>
{
    private readonly IExternalAccountLinkService _service;

    public CreateExternalAccountTransactionEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/external-accounts/transactions");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CreateExternalAccountTransactionRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.CreateTransactionAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── Transaction Attachments ──────────────────────────────────────

internal sealed class UploadExternalAccountTransactionAttachmentEndpoint
    : EndpointWithoutRequest<ExternalAccountTransactionAttachmentResponse>
{
    private readonly IExternalAccountLinkService _service;

    public UploadExternalAccountTransactionAttachmentEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/external-accounts/transactions/{transactionId}/attachments");
        Policies("AdminPolicy");
        AllowFileUploads();
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
            var response = await _service.AddTransactionAttachmentAsync(
                transactionId,
                stream,
                file.FileName,
                contentType,
                ct);

            await Send.OkAsync(response, ct);
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

internal sealed class ListExternalAccountTransactionAttachmentsEndpoint
    : EndpointWithoutRequest<IReadOnlyList<ExternalAccountTransactionAttachmentResponse>>
{
    private readonly IExternalAccountLinkService _service;

    public ListExternalAccountTransactionAttachmentsEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/external-accounts/transactions/{transactionId}/attachments");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var transactionId = Route<Guid>("transactionId");
        var response = await _service.ListTransactionAttachmentsAsync(transactionId, ct);
        await Send.OkAsync(response, ct);
    }
}

internal sealed class DeleteExternalAccountTransactionAttachmentEndpoint : EndpointWithoutRequest
{
    private readonly IExternalAccountLinkService _service;

    public DeleteExternalAccountTransactionAttachmentEndpoint(IExternalAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Delete("/admin/external-accounts/attachments/{attachmentId}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var attachmentId = Route<Guid>("attachmentId");
            await _service.DeleteTransactionAttachmentAsync(attachmentId, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }
}
