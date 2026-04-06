using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetPersonalTransactionEndpoint : EndpointWithoutRequest<PersonalTransactionResponse>
{
    private readonly IPersonalTransactionService _personalTransactionService;

    public GetPersonalTransactionEndpoint(IPersonalTransactionService personalTransactionService)
    {
        _personalTransactionService = personalTransactionService;
    }

    public override void Configure()
    {
        Get("/personal-finance/transactions/{id}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a personal transaction by ID";
            s.Description = "Returns the full details of a single personal transaction including amount, category, merchant, and classification status.";
            s.Response(200, "Transaction returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Transaction not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var response = await _personalTransactionService.GetTransactionAsync(id, ct);

        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
