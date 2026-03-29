using Aonik.Finance.Contracts.Models.Accounts;
using Aonik.Finance.Contracts.Services.Accounts;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Admin.Accounts;

internal sealed class ListAccountConnectionsRequest
{
    public bool IncludeDisconnected { get; set; }
}

internal sealed class CreateAccountLinkSessionEndpoint : Endpoint<CreateAccountLinkSessionRequest, AccountLinkSessionResponse>
{
    private readonly IAccountLinkService _service;

    public CreateAccountLinkSessionEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/accounts/connections/sessions");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CreateAccountLinkSessionRequest req, CancellationToken ct)
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

internal sealed class ExchangeAccountLinkSessionEndpoint : Endpoint<ExchangeAccountLinkSessionRequest, AccountLinkExchangeResponse>
{
    private readonly IAccountLinkService _service;

    public ExchangeAccountLinkSessionEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/accounts/connections/exchanges");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ExchangeAccountLinkSessionRequest req, CancellationToken ct)
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

internal sealed class ListAccountConnectionsEndpoint : Endpoint<ListAccountConnectionsRequest, IReadOnlyList<AccountConnectionResponse>>
{
    private readonly IAccountLinkService _service;

    public ListAccountConnectionsEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/accounts/connections");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ListAccountConnectionsRequest req, CancellationToken ct)
    {
        var response = await _service.ListConnectionsAsync(req.IncludeDisconnected, ct);
        await Send.OkAsync(response, ct);
    }
}

internal sealed class RefreshAccountConnectionEndpoint : EndpointWithoutRequest<AccountLinkActionResponse>
{
    private readonly IAccountLinkService _service;

    public RefreshAccountConnectionEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/accounts/connections/{id}/refresh");
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

            await Send.OkAsync(new AccountLinkActionResponse("refresh", response), ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

internal sealed class DisconnectAccountConnectionEndpoint : EndpointWithoutRequest<AccountLinkActionResponse>
{
    private readonly IAccountLinkService _service;

    public DisconnectAccountConnectionEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/accounts/connections/{id}/disconnect");
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

        await Send.OkAsync(new AccountLinkActionResponse("disconnect", response), ct);
    }
}

internal sealed class SyncAccountTransactionsEndpoint : EndpointWithoutRequest<AccountTransactionSyncResponse>
{
    private readonly IAccountLinkService _service;

    public SyncAccountTransactionsEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/accounts/connections/{id}/transactions/sync");
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

internal sealed class ListAccountTransactionsRequest
{
    public Guid? ExternalAccountId { get; set; }
    public Guid? ConnectionId { get; set; }
    public string? ReconciliationStatus { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

internal sealed class ListAccountTransactionsEndpoint : Endpoint<ListAccountTransactionsRequest, PagedResult<AccountTransactionResponse>>
{
    private readonly IAccountLinkService _service;

    public ListAccountTransactionsEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/accounts/transactions");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ListAccountTransactionsRequest req, CancellationToken ct)
    {
        var response = await _service.ListTransactionsAsync(
            new Contracts.Models.Accounts.ListAccountTransactionsRequest(
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

internal sealed class PlaidAccountWebhookEndpoint : Endpoint<PlaidAccountWebhookRequest, AccountLinkWebhookResponse>
{
    private readonly IAccountLinkService _service;

    public PlaidAccountWebhookEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/accounts/webhooks/plaid");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PlaidAccountWebhookRequest req, CancellationToken ct)
    {
        await _service.ProcessPlaidWebhookAsync(req, ct);
        await Send.OkAsync(new AccountLinkWebhookResponse("accepted"), ct);
    }
}

// ── Manual Account CRUD ──────────────────────────────────────────

internal sealed class CreateAccountEndpoint : Endpoint<CreateAccountRequest, AccountResponse>
{
    private readonly IAccountLinkService _service;

    public CreateAccountEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/accounts");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CreateAccountRequest req, CancellationToken ct)
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

internal sealed class ListAccountsEndpoint : EndpointWithoutRequest<IReadOnlyList<AccountResponse>>
{
    private readonly IAccountLinkService _service;

    public ListAccountsEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/accounts");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _service.ListAccountsAsync(ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Manual Transaction CRUD ──────────────────────────────────────

internal sealed class CreateAccountTransactionEndpoint : Endpoint<CreateAccountTransactionRequest, AccountTransactionResponse>
{
    private readonly IAccountLinkService _service;

    public CreateAccountTransactionEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/accounts/transactions");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CreateAccountTransactionRequest req, CancellationToken ct)
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

internal sealed class UploadAccountTransactionAttachmentEndpoint
    : EndpointWithoutRequest<AccountTransactionAttachmentResponse>
{
    private readonly IAccountLinkService _service;

    public UploadAccountTransactionAttachmentEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/accounts/transactions/{transactionId}/attachments");
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

internal sealed class ListAccountTransactionAttachmentsEndpoint
    : EndpointWithoutRequest<IReadOnlyList<AccountTransactionAttachmentResponse>>
{
    private readonly IAccountLinkService _service;

    public ListAccountTransactionAttachmentsEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/accounts/transactions/{transactionId}/attachments");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var transactionId = Route<Guid>("transactionId");
        var response = await _service.ListTransactionAttachmentsAsync(transactionId, ct);
        await Send.OkAsync(response, ct);
    }
}

internal sealed class DeleteAccountTransactionAttachmentEndpoint : EndpointWithoutRequest
{
    private readonly IAccountLinkService _service;

    public DeleteAccountTransactionAttachmentEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Delete("/admin/accounts/attachments/{attachmentId}");
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
