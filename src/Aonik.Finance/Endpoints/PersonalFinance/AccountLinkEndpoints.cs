using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class ListAccountLinksRequest
{
    public bool IncludeDisconnected { get; set; }
}

internal sealed class AccountLinkSummaryRequest
{
    public bool IncludeArchived { get; set; }
}

internal sealed class CreateAccountLinkSessionEndpoint : Endpoint<CreateAccountLinkSessionRequest, AccountLinkSessionResponse>
{
    private readonly IPersonalAccountLinkService _personalAccountLinkService;

    public CreateAccountLinkSessionEndpoint(IPersonalAccountLinkService personalAccountLinkService)
    {
        _personalAccountLinkService = personalAccountLinkService;
    }

    public override void Configure()
    {
        Post("/personal-finance/account-links/sessions");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CreateAccountLinkSessionRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _personalAccountLinkService.CreateSessionAsync(req, ct);
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
    private readonly IPersonalAccountLinkService _personalAccountLinkService;

    public ExchangeAccountLinkSessionEndpoint(IPersonalAccountLinkService personalAccountLinkService)
    {
        _personalAccountLinkService = personalAccountLinkService;
    }

    public override void Configure()
    {
        Post("/personal-finance/account-links/exchanges");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(ExchangeAccountLinkSessionRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _personalAccountLinkService.ExchangeSessionAsync(req, ct);
            if (response == null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

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

internal sealed class ListAccountLinksEndpoint : Endpoint<ListAccountLinksRequest, IReadOnlyList<AccountLinkConnectionResponse>>
{
    private readonly IPersonalAccountLinkService _personalAccountLinkService;

    public ListAccountLinksEndpoint(IPersonalAccountLinkService personalAccountLinkService)
    {
        _personalAccountLinkService = personalAccountLinkService;
    }

    public override void Configure()
    {
        Get("/personal-finance/account-links");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(ListAccountLinksRequest req, CancellationToken ct)
    {
        var response = await _personalAccountLinkService.ListConnectionsAsync(req.IncludeDisconnected, ct);
        await Send.OkAsync(response, ct);
    }
}

internal sealed class GetAccountLinksSummaryEndpoint : Endpoint<AccountLinkSummaryRequest, IReadOnlyList<AccountLinkSummaryItemResponse>>
{
    private readonly IPersonalAccountLinkService _personalAccountLinkService;

    public GetAccountLinksSummaryEndpoint(IPersonalAccountLinkService personalAccountLinkService)
    {
        _personalAccountLinkService = personalAccountLinkService;
    }

    public override void Configure()
    {
        Get("/personal-finance/account-links/summary");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(AccountLinkSummaryRequest req, CancellationToken ct)
    {
        var response = await _personalAccountLinkService.GetSummaryAsync(req.IncludeArchived, ct);
        await Send.OkAsync(response, ct);
    }
}

internal sealed class RefreshAccountLinkEndpoint : EndpointWithoutRequest<AccountLinkActionResponse>
{
    private readonly IPersonalAccountLinkService _personalAccountLinkService;

    public RefreshAccountLinkEndpoint(IPersonalAccountLinkService personalAccountLinkService)
    {
        _personalAccountLinkService = personalAccountLinkService;
    }

    public override void Configure()
    {
        Post("/personal-finance/account-links/{id}/refresh");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var id = Route<Guid>("id");
            var response = await _personalAccountLinkService.RefreshConnectionAsync(id, ct);
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

internal sealed class DisconnectAccountLinkEndpoint : EndpointWithoutRequest<AccountLinkActionResponse>
{
    private readonly IPersonalAccountLinkService _personalAccountLinkService;

    public DisconnectAccountLinkEndpoint(IPersonalAccountLinkService personalAccountLinkService)
    {
        _personalAccountLinkService = personalAccountLinkService;
    }

    public override void Configure()
    {
        Post("/personal-finance/account-links/{id}/disconnect");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var response = await _personalAccountLinkService.DisconnectConnectionAsync(id, ct);
        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new AccountLinkActionResponse("disconnect", response), ct);
    }
}

internal sealed class SyncAccountLinkTransactionsEndpoint : EndpointWithoutRequest<AccountLinkTransactionSyncResponse>
{
    private readonly IPersonalAccountLinkService _personalAccountLinkService;

    public SyncAccountLinkTransactionsEndpoint(IPersonalAccountLinkService personalAccountLinkService)
    {
        _personalAccountLinkService = personalAccountLinkService;
    }

    public override void Configure()
    {
        Post("/personal-finance/account-links/{id}/transactions/sync");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var id = Route<Guid>("id");
            var response = await _personalAccountLinkService.SyncConnectionTransactionsAsync(id, ct);
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

internal sealed class PlaidAccountLinkWebhookEndpoint : Endpoint<PlaidAccountLinkWebhookRequest, AccountLinkWebhookResponse>
{
    private readonly IPersonalAccountLinkService _personalAccountLinkService;

    public PlaidAccountLinkWebhookEndpoint(IPersonalAccountLinkService personalAccountLinkService)
    {
        _personalAccountLinkService = personalAccountLinkService;
    }

    public override void Configure()
    {
        Post("/personal-finance/account-links/webhooks/plaid");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PlaidAccountLinkWebhookRequest req, CancellationToken ct)
    {
        await _personalAccountLinkService.ProcessPlaidWebhookAsync(req, ct);
        await Send.OkAsync(new AccountLinkWebhookResponse("accepted"), ct);
    }
}
