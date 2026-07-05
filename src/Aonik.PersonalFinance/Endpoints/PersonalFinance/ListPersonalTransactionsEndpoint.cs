using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class ListPersonalTransactionsEndpoint : Endpoint<ListPersonalTransactionsRequest, IReadOnlyList<PersonalTransactionResponse>>
{
    private readonly IPersonalTransactionService _personalTransactionService;

    public ListPersonalTransactionsEndpoint(IPersonalTransactionService personalTransactionService)
    {
        _personalTransactionService = personalTransactionService;
    }

    public override void Configure()
    {
        Get("/personal-finance/transactions");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List personal transactions";
            s.Description = "Returns personal transactions with optional filters for account, category, date range, and search terms.";
            s.Response(200, "Transaction list returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(ListPersonalTransactionsRequest req, CancellationToken ct)
    {
        var response = await _personalTransactionService.ListTransactionsAsync(req, ct);
        await Send.OkAsync(response, ct);
    }
}
