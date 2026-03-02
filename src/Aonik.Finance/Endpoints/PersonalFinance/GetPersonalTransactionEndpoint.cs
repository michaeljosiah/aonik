using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

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
