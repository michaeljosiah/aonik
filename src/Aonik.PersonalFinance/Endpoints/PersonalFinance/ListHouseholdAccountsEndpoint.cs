using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class ListHouseholdAccountsEndpoint : EndpointWithoutRequest<IReadOnlyList<PersonalAccountResponse>>
{
    private readonly IPersonalAccountService _personalAccountService;

    public ListHouseholdAccountsEndpoint(IPersonalAccountService personalAccountService)
    {
        _personalAccountService = personalAccountService;
    }

    public override void Configure()
    {
        Get("/personal-finance/households/accounts");
        Policies("UserPolicy");
        Summary(s => s.Summary = "List household accounts");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var response = await _personalAccountService.ListHouseholdAccountsAsync(ct);
            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 409);
        }
    }
}
