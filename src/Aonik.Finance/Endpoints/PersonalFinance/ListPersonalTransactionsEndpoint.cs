using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
    }

    public override async Task HandleAsync(ListPersonalTransactionsRequest req, CancellationToken ct)
    {
        var response = await _personalTransactionService.ListTransactionsAsync(req, ct);
        await Send.OkAsync(response, ct);
    }
}
