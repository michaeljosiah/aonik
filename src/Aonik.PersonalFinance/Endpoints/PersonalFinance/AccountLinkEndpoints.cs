using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Create an account link session";
            s.Description = "Initiates a new account linking session with a financial data provider (e.g. Plaid) to connect external bank accounts.";
            s.Response(200, "Link session created successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Exchange an account link session";
            s.Description = "Exchanges a completed link session token for permanent account connection credentials and creates linked personal accounts.";
            s.Response(200, "Session exchanged successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error or invalid session");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(ExchangeAccountLinkSessionRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _personalAccountLinkService.ExchangeSessionAsync(req, ct);
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
        Summary(s =>
        {
            s.Summary = "List account link connections";
            s.Description = "Returns all linked external account connections, with an option to include previously disconnected links.";
            s.Response(200, "Account links returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Get account links summary";
            s.Description = "Returns a summary of all linked accounts grouped by connection, including balances and sync status.";
            s.Response(200, "Account links summary returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Refresh an account link";
            s.Description = "Triggers a refresh of account data from the external provider, updating balances and fetching new transactions.";
            s.Response(200, "Account link refreshed successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Account link not found");
            s.Response(422, "Refresh failed or action required from provider");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        catch (AccountLinkActionRequiredException ex)
        {
            var response = new AccountLinkActionRequiredErrorResponse(
                "account_link_action_required",
                ex.Message,
                ex.RequiredAction,
                ex.RequiresReconnect,
                ex.ConnectionId,
                ex.Provider,
                ex.ProviderErrorCode);

            await TypedResults.UnprocessableEntity(response).ExecuteAsync(HttpContext);
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
        Summary(s =>
        {
            s.Summary = "Disconnect an account link";
            s.Description = "Disconnects an external account link, stopping automatic data syncing while preserving previously imported data.";
            s.Response(200, "Account link disconnected successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Account link not found");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Sync account link transactions";
            s.Description = "Manually triggers a transaction sync for a linked account, importing new transactions from the external provider.";
            s.Response(200, "Transactions synced successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Account link not found");
            s.Response(422, "Sync failed or connection in error state");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Receive Plaid webhook";
            s.Description = "Processes incoming webhook notifications from Plaid for account link events such as transaction updates and connection status changes.";
            s.Response(200, "Webhook processed successfully");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(PlaidAccountLinkWebhookRequest req, CancellationToken ct)
    {
        await _personalAccountLinkService.ProcessPlaidWebhookAsync(req, ct);
        await Send.OkAsync(new AccountLinkWebhookResponse("accepted"), ct);
    }
}
